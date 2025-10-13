namespace Skynet.Core.ResourceProvider;

public sealed class ResourceListResult : IResourceListResult
{
    public ResourceRequest Request { get; }
    public IReadOnlyList<string> Keys { get; }
    public string? ContinuationToken { get; }
    public ProviderId? ProviderId { get; }

    public ResourceListResult(
        ResourceRequest request,
        IReadOnlyList<string> keys,
        string? continuationToken = null,
        ProviderId? providerId = null)
        => (Request, Keys, ContinuationToken, ProviderId)
            = (request, keys, continuationToken, providerId);
}