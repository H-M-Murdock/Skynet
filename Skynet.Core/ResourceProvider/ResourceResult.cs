using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>Default sealed implementation of <see cref="IResourceResult"/>.</summary>
public sealed class ResourceResult : IResourceResult
{
    public ResourceResult(
        TenantId tenantId,
        string key,
        Stream content,
        string? contentType = null,
        DateTimeOffset? lastModified = null,
        long? contentLength = null,
        string? version = null)
    {
        TenantId = tenantId;
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = contentType;
        LastModified = lastModified;
        ContentLength = contentLength;
        Version = version;
    }

    public TenantId TenantId { get; }
    public string Key { get; }
    public Stream Content { get; }
    public string? ContentType { get; }
    public DateTimeOffset? LastModified { get; }
    public long? ContentLength { get; }
    public string? Version { get; }

    public void Dispose() => Content.Dispose();
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}