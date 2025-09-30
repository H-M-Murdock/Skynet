// Skynet.Core/ResourceProvider/ResourceLocator.cs
namespace Skynet.Core.ResourceProvider;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Localization;
using Skynet.Core.Tenant;

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
    /// providers: Reihenfolge definiert die Lookup-Priorität der Provider.
    /// cultureScopeFactory: optional; eröffnet für Anfragen einen Culture-Scope (mit Override, falls angegeben).
    /// Verwendet ProgramTenantContext.Instance als Tenant-Quelle (Singleton, nicht via DI).
    /// </summary>
    public ResourceLocator(
        IEnumerable<IResourceProvider> providers,
        ICultureThreadScopeFactory? cultureScopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = [..providers];
        _tenantContext = ProgramTenantContext.Instance; // Singleton verwenden
        _cultureScopeFactory = cultureScopeFactory; 
    }

    // Convenience: Overloads ohne Options delegieren auf die Options-Varianten
    public Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => GetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => TryGetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public async Task<IResourceResult> GetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        var res = await TryGetAsync(request, options, cancellationToken).ConfigureAwait(false);
        if (res.Status is ResourceLookupStatus.Found or ResourceLookupStatus.NotModified)
            return res.Resource!;

        var chain = string.Join("->", _tenantContext.ResolutionChain.Select(t => t.ToString()));
        throw new KeyNotFoundException($"Resource not found for key '{request.Key}' (tenant chain: {chain})");
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
                scope = options?.CultureOverride is { } ci
                    ? _cultureScopeFactory.BeginScope(ci)
                    : _cultureScopeFactory.BeginScope();
            }

            foreach (var tenant in _tenantContext.ResolutionChain)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scoped = request with { TenantId = tenant };

                foreach (var provider in _providers)
                {
                    if (!provider.CanHandle(scoped)) continue;

                    var lookup = await provider.TryGetAsync(scoped, cancellationToken).ConfigureAwait(false);

                    switch (lookup.Status)
                    {
                        case ResourceLookupStatus.Found:
                        case ResourceLookupStatus.NotModified: return lookup;
                        case ResourceLookupStatus.NotFound:
                        default:
                            // Nächsten Provider / nächsten Tenant versuchen
                            break;
                    }
                }
            }

            return ResourceLookupResult.NotFound();
        }
        finally
        {
            scope?.Dispose();
        }
    }
}
