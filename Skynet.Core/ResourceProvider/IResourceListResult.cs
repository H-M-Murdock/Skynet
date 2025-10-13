using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public interface IResourceListResult
{
    ResourceRequest Request { get; }
    IReadOnlyList<string> Keys { get; }
    string? ContinuationToken { get; }
    ProviderId? ProviderId { get; }
}