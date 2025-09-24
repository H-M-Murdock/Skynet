// Skynet.Core/ResourceProvider/IResourceProvider.cs
namespace Skynet.Core.ResourceProvider;

using System.Threading;
using System.Threading.Tasks;

public interface IResourceProvider
{
    bool CanHandle(ResourceRequest request);

    ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);
}