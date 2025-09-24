// Skynet.Core/ResourceProvider/IResourceLocator.cs
namespace Skynet.Core.ResourceProvider;

using System.Threading;
using System.Threading.Tasks;

public interface IResourceLocator
{
    Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default);
    Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default);

    Task<IResourceResult> GetAsync(ResourceRequest request, ResourceQueryOptions options, CancellationToken cancellationToken = default);
    Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, ResourceQueryOptions options, CancellationToken cancellationToken = default);
}