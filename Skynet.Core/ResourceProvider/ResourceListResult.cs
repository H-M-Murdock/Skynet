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
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Keys = keys ?? throw new ArgumentNullException(nameof(keys));
        ContinuationToken = continuationToken;
        ProviderId = providerId;
    }

    public override string ToString()
    {
        var providerInfo = ProviderId.HasValue ? ProviderId.ToString() : "aggregated";
        var tokenInfo = ContinuationToken != null ? " (+more)" : "";
        return $"ResourceListResult {{ Keys = {Keys.Count}, Source = {providerInfo}{tokenInfo} }}";
    }
}