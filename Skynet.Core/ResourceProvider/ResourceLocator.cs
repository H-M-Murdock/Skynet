// Skynet.Core/ResourceProvider/ResourceLocator.cs
namespace Skynet.Core.ResourceProvider;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Localization;
using Tenant;

/// <summary>
/// Orchestriert Ressourcensuchen (READ) über eine geordnete Menge von IResourceProvider
/// entlang der vom ITenantContext vorgegebenen Tenant-Resolution-Chain und
/// optional Ressourcen-Mutationen (WRITE/DELETE) über registrierte IResourceWriter.
/// </summary>
public sealed class ResourceLocator : IResourceLocator
{
    private readonly ImmutableArray<IResourceReader> _readProviders;
    private readonly ImmutableArray<IResourceWriter> _writeProviders;
    private readonly ITenantContext _tenantContext;
    private readonly ICultureThreadScopeFactory? _cultureScopeFactory;

    /// <summary>
    /// Erstellt einen ResourceLocator.
    /// providers: Read-Provider; Reihenfolge wird nach Priority sortiert (kleiner = früher).
    /// writers: Write-Provider; optional. Ebenfalls nach Priority sortiert, sofern sie CanHandle unterstützen.
    /// tenantContext: optional; wenn null, ProgramTenantContext.Instance als Fallback.
    /// cultureScopeFactory: optional; setzt (bei READ) einen Culture-Scope (Default oder Override).
    /// </summary>
    public ResourceLocator(
        IEnumerable<IResourceReader> providers,
        IEnumerable<IResourceWriter>? writers = null,
        ITenantContext? tenantContext = null,
        ICultureThreadScopeFactory? cultureScopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _readProviders = [..providers.OrderBy(p => p.Priority)];
        _writeProviders = writers is null ? ImmutableArray<IResourceWriter>.Empty : [..writers];
        _tenantContext = tenantContext ?? ProgramTenantContext.Instance;
        _cultureScopeFactory = cultureScopeFactory;
    }

    // -------------------- READ --------------------

    public Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => GetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => TryGetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CultureInfo? culture, CancellationToken cancellationToken = default)
        => TryGetAsync(request, new ResourceQueryOptions(culture), cancellationToken);

    public async Task<IResourceResult> GetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var res = await TryGetAsync(request, options, cancellationToken).ConfigureAwait(false);
        if (res.Status is ResourceLookupStatus.Found or ResourceLookupStatus.NotModified)
            return res.Resource!;

        // Diagnose: TenantChain + Providerliste + Reason
        var chain = string.Join(" -> ", _tenantContext.ResolutionChain.Select(t => t.ToString()));
        var providerList = _readProviders.Length == 0
            ? "(keine Provider registriert)"
            : string.Join(", ", _readProviders.Select(p => p.Id.ToString()));

        var reasonPart = string.IsNullOrWhiteSpace(res.Reason)
            ? "Kein Provider hat eine Reason gemeldet."
            : $"Letzte Reason: {res.Reason}";

        var message = new StringBuilder()
            .AppendLine($"Ressource nicht gefunden für Key '{request.Key}'.")
            .AppendLine($"Tenant-Chain: [{chain}]")
            .AppendLine($"Geprüfte Provider: [{providerList}]")
            .AppendLine(reasonPart)
            .ToString();

        throw new KeyNotFoundException(message);
    }

    public async Task<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        IDisposable? scope = null;
        try
        {
            if (_cultureScopeFactory is not null)
            {
                scope = options.CultureOverride is { } ci
                    ? _cultureScopeFactory.BeginScope(ci)
                    : _cultureScopeFactory.BeginScope();
            }

            string? lastReason = null;

            // Vorfilterung: nur Provider, die (für CurrentTenant) grundsätzlich können
            var currentTenant = _tenantContext.ResolutionChain.FirstOrDefault();

            ImmutableArray<IResourceReader> filteredProviders = _readProviders;
            if (currentTenant is { } ct)
            {
                var probeRequest = request with { TenantId = ct };
                filteredProviders = [.._readProviders.Where(p => p.CanHandle(probeRequest))];
            }

            foreach (var tenant in _tenantContext.ResolutionChain)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scoped = request with { TenantId = tenant };

                foreach (var provider in filteredProviders)
                {
                    if (!provider.CanHandle(scoped)) continue;

                    var lookup = await provider.TryGetAsync(scoped, cancellationToken).ConfigureAwait(false);

                    switch (lookup.Status)
                    {
                        case ResourceLookupStatus.Found:
                        case ResourceLookupStatus.NotModified:
                            return lookup;

                        case ResourceLookupStatus.NotFound:
                            lastReason = lookup.Reason ?? lastReason;
                            break;

                        default:
                            break;
                    }
                }
            }

            return ResourceLookupResult.NotFound(lastReason);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    // -------------------- WRITE --------------------

    public async Task<IResourceWriteResult> WriteAsync(
        ResourceRequest request,
        Stream content,
        bool createIfMissing = true,
        string? ifMatch = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Auswahl: erster Writer, der diese Resource (Kind) schreibt.
        // Optional könnte IResourceWriter CanHandle besitzen; hier einfache Routing-Variante.
        var writer = SelectWriter(request);
        if (writer is null)
            throw new NotSupportedException($"Kein schreibfähiger Provider für ResourceKind '{request.ResourceType}' registriert.");

        return await writer.WriteAsync(request, content, createIfMissing, ifMatch, contentType, cancellationToken)
                           .ConfigureAwait(false);
    }

    public async Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        var writer = SelectWriter(request);
        if (writer is null)
            throw new NotSupportedException($"Kein schreibfähiger Provider für ResourceKind '{request.ResourceType}' registriert.");

        return await writer.DeleteAsync(request, ifMatch, cancellationToken).ConfigureAwait(false);
    }

    // Auswahlstrategie für Writer: der erste registrierte Writer (Priority-Ranking kann in Writer selbst abgebildet werden).
    private IResourceWriter? SelectWriter(ResourceRequest request)
    {
        // Falls mehrere Writer mit Priorität, hier nach Policy ordnen/filtern.
        // Z. B. Writers mit optionalem CanHandle(request) bevorzugen.
        return _writeProviders.FirstOrDefault();
    }
    
    
    public async Task<IResourceListResult> ListKeysAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keys = new HashSet<string>(StringComparer.Ordinal);
        string? continuationToken = null;
        ProviderId? lastProviderId = null;

        var currentTenant = _tenantContext.ResolutionChain.FirstOrDefault();
        ImmutableArray<IResourceReader> filteredProviders = _readProviders;
        if (currentTenant is { } ct)
        {
            var probe = request with { TenantId = ct };
            filteredProviders = [.._readProviders.Where(p => p.CanHandle(probe))];
        }

        foreach (var tenant in _tenantContext.ResolutionChain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scoped = request with { TenantId = tenant };

            foreach (var provider in filteredProviders)
            {
                if (!provider.CanHandle(scoped)) continue;

                var (pageKeys, nextToken) = await provider.ListKeysAsync(
                        scoped,
                        continuationToken: continuationToken,
                        limit: null,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                foreach (var k in pageKeys)
                    keys.Add(k);

                if (!string.IsNullOrEmpty(nextToken))
                {
                    continuationToken = nextToken;
                    lastProviderId = provider.Id;
                }
            }
        }

        var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        return new ResourceListResult(request, sorted, continuationToken, lastProviderId);
    }
    
    
    
}