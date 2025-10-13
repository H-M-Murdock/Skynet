using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;

/// <summary>
/// Read-only Metadaten eines erfolgreichen Schreibvorgangs, analog zu IResourceResult (ohne Content).
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