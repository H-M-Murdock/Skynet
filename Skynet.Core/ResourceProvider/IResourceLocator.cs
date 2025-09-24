namespace Skynet.Core.ResourceProvider;

public interface IResourceLocator
{
    Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default);
    Task<(bool found, IResourceResult? result)> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default);

    // Overloads with options (e.g., culture override)
    Task<IResourceResult> GetAsync(ResourceRequest request, ResourceQueryOptions options, CancellationToken cancellationToken = default);
    Task<(bool found, IResourceResult? result)> TryGetAsync(ResourceRequest request, ResourceQueryOptions options, CancellationToken cancellationToken = default);
}