using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Read-only contract for a resolved resource (content + metadata).
/// This is the *result of locating* a resource, not the domain object itself.
/// </summary>
public interface IResourceResult : IDisposable, IAsyncDisposable
{
    TenantId TenantId { get; }
    string Key { get; }
    Stream Content { get; }
    string? ContentType { get; }
    DateTimeOffset? LastModified { get; }
    long? ContentLength { get; }
    /// <summary>Opaque concurrency/version token (ETag). Changes when the content changes.</summary>
    string? Version { get; }

    /// <summary>Identität des Providers, der dieses Ergebnis geliefert hat (für Logging/Tracing/Cache).</summary>
    ProviderId? ProviderId { get; }
}