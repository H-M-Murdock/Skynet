// Skynet.Core/ResourceProvider/ResourceLocator.cs
namespace Skynet.Core.ResourceProvider;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Localization;
using Tenant;
using System.Globalization;

/// <summary>
/// Orchestriert Ressourcensuchen über eine geordnete Menge von IResourceProvider
/// und entlang der vom ITenantContext vorgegebenen Tenant-Resolution-Chain.
/// Bietet TryGetAsync (ohne Exceptions bei Nichttreffer) sowie GetAsync (wirft bei Nichtfund).
/// Optional wird für die Dauer des Lookups ein Culture-Scope gesetzt (Default oder Override).
/// </summary>
public sealed class ResourceLocator : IResourceLocator
{
    private readonly ImmutableArray<IResourceProvider> _providers;
    private readonly ITenantContext _tenantContext;
    private readonly ICultureThreadScopeFactory? _cultureScopeFactory;

    /// <summary>
    /// Erstellt einen ResourceLocator.
    /// Providers: Reihenfolge definiert die Lookup-Priorität der Provider.
    /// tenantContext: optional; wenn nicht angegeben, wird ProgramTenantContext.Instance verwendet (eindeutiger System-Tenant).
    /// cultureScopeFactory: optional; eröffnet für Anfragen einen Culture-Scope (mit Override, falls angegeben).
    /// </summary>
    public ResourceLocator(
        IEnumerable<IResourceProvider> providers,
        ITenantContext? tenantContext = null,
        ICultureThreadScopeFactory? cultureScopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = [..providers.OrderBy(p => p.Priority)];
        _tenantContext = tenantContext ?? ProgramTenantContext.Instance; // System-Singleton als Fallback
        _cultureScopeFactory = cultureScopeFactory; 
    }

    // Convenience: Overloads ohne Options delegieren auf die Options-Varianten
    public Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => GetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => TryGetAsync(request, new ResourceQueryOptions(), cancellationToken);

    // Bequemlichkeits-Overload: nur Kultur setzen
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

        // Diagnostik aufbauen: TenantChain, Provider-Liste, letzte Reason
        var chain = string.Join(" -> ", _tenantContext.ResolutionChain.Select(t => t.ToString()));
        var providerList = _providers.Length == 0 
            ? "(keine Provider registriert)" 
            : string.Join(", ", _providers.Select(p => p.Id.ToString()));
        
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
            // Kultur-Scope optional öffnen (Default oder Override)
            if (_cultureScopeFactory is not null)
            {
                scope = options.CultureOverride is { } ci
                    ? _cultureScopeFactory.BeginScope(ci)
                    : _cultureScopeFactory.BeginScope();
            }

            string? lastReason = null;

            // Vorfilterung mit Priority-Order beibehalten
            var currentTenant = _tenantContext.ResolutionChain.FirstOrDefault();
            ImmutableArray<IResourceProvider> filteredProviders;
            if (currentTenant is { } ct)
            {
                var probeRequest = request with { TenantId = ct };
                filteredProviders = [.._providers.Where(p => p.CanHandle(probeRequest))];
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
                            // Sammle letzte Reason (überschreibt vorherige)
                            lastReason = lookup.Reason ?? lastReason;
                            break;

                        default:
                            break;
                    }
                }
            }

            // TryGetAsync gibt bei NotFound die letzte gesammelte Reason zurück
            return ResourceLookupResult.NotFound(lastReason);
        }
        finally
        {
            scope?.Dispose();
        }
    }
}
