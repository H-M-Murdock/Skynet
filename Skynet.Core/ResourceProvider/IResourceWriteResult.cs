using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Read-only Metadaten eines erfolgreichen Schreibvorgangs.
/// Enthält alles außer dem Content selbst (analog zu IResourceResult).
/// </summary>
public interface IResourceWriteResult
{
    TenantId TenantId { get; }
    string Key { get; }
    string? ContentType { get; }
    DateTimeOffset? LastModified { get; }
    long? ContentLength { get; }
    
    /// <summary>Neues ETag/Version der Ressource.</summary>
    string? Version { get; }
    
    ProviderId? ProviderId { get; }
}

/// <summary>
/// Standard-Implementierung für IResourceWriteResult.
/// Erspart den Providern das Schreiben eigener DTO-Klassen.
/// </summary>
public sealed class ResourceWriteResult : IResourceWriteResult
{
    public TenantId TenantId { get; }
    public string Key { get; }
    public string? ContentType { get; }
    public DateTimeOffset? LastModified { get; }
    public long? ContentLength { get; }
    public string? Version { get; }
    public ProviderId? ProviderId { get; }

    public ResourceWriteResult(
        TenantId tenantId,
        string key,
        string? version,
        DateTimeOffset? lastModified = null,
        long? contentLength = null,
        ProviderId? providerId = null,
        string? contentType = null)
    {
        TenantId = tenantId;
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Version = version;
        LastModified = lastModified;
        ContentLength = contentLength;
        ProviderId = providerId;
        ContentType = contentType;
    }

    public override string ToString()
        => $"ResourceWriteResult {{ Key={Key}, Version={Version}, Size={ContentLength}, Provider={ProviderId?.ToString() ?? "N/A"} }}";
}