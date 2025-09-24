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

public sealed class ResourceLocator : IResourceLocator
{
    private readonly ImmutableArray<IResourceProvider> _providers;
    private readonly ITenantContext _tenantContext;
    private readonly ICultureThreadScopeFactory? _cultureScopeFactory;

    public ResourceLocator(
        IEnumerable<IResourceProvider> providers,
        ITenantContext tenantContext,
        ICultureThreadScopeFactory? cultureScopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToImmutableArray();
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _cultureScopeFactory = cultureScopeFactory; // optional
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
                        case ResourceLookupStatus.NotModified:
                            // Tenant auffüllen, falls Provider ihn nicht gesetzt hat
                            return lookup.ResolvedTenant is null
                                ? ResourceLookupResult.Found(
                                    lookup.Resource!,
                                    provider: lookup.Provider,
                                    t: tenant) 
                                : lookup;

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
