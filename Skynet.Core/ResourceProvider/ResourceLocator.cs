namespace Skynet.Core.ResourceProvider;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Tenant;
using Skynet.Core.Localization;

/// <summary>
/// The locator iterates the tenant resolution chain and provider list in order, returning the first hit.
/// It propagates cancellation correctly and wraps the operation in an optional culture scope,
/// so providers can read the ambient CultureInfo without polluting the core request contract.
/// </summary>
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
        _providers = [..providers];
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _cultureScopeFactory = cultureScopeFactory; // optional
    }

    public Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => GetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public Task<(bool found, IResourceResult? result)> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => TryGetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public async Task<IResourceResult> GetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        var (found, result) = await TryGetAsync(request, options, cancellationToken).ConfigureAwait(false);
        if (found) return result!;
        var chain = string.Join("->", _tenantContext.ResolutionChain.Select(t => t.ToString()));
        throw new KeyNotFoundException($"Resource not found for key '{request.Key}' (tenant chain: {chain})");
    }

    public async Task<(bool found, IResourceResult? result)> TryGetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        // Öffne optional einen Culture-Scope – nur wenn Factory vorhanden
        IDisposable? scope = null;
        try
        {
            if (_cultureScopeFactory is not null)
            {
                scope = options?.CultureOverride is { } ci
                    ? _cultureScopeFactory.BeginScope(ci)
                    : _cultureScopeFactory.BeginScope();
            }

            foreach (var tenant in _tenantContext.ResolutionChain)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scopedRequest = request with { TenantId = tenant };

                // definierte Provider-Reihenfolge – erster Treffer gewinnt
                foreach (var provider in _providers)
                {
                    if (!provider.CanHandle(scopedRequest))
                        continue;

                    var (found, result) = await provider
                        .TryGetAsync(scopedRequest, cancellationToken)
                        .ConfigureAwait(false);

                    if (found)
                        return (true, result);
                }
            }

            return (false, null);
        }
        finally
        {
            scope?.Dispose();
        }
    }
}
