using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Übergabeobjekt für Parser mit Quelle + Kontext.
/// </summary>
public sealed class ResourceEnvelope
{
    /// <summary>Logischer Schlüssel (tenant-neutral; Tenant im Kontext).</summary>
    public string LogicalKey { get; }

    /// <summary>Vom Provider gemeldeter ContentType (kann null/ungenau sein).</summary>
    public string? ContentType { get; }

    /// <summary>Inhalt als Stream; Implementierungen können bei Bedarf Position auf 0 setzen.</summary>
    public Stream Content { get; }

    /// <summary>Aktiver Tenant-Kontext der Auflösung.</summary>
    public ITenantContext Tenant { get; }

    /// <summary>Identität des liefernden Providers (Diagnostik/Tracing), optional.</summary>
    public ProviderId? ProviderId { get; }

    /// <summary>Version/ETag der Quelle (optimistisches Locking/Diagnostik), optional.</summary>
    public string? Version { get; }

    public ResourceEnvelope(
        string logicalKey,
        string? contentType,
        Stream content,
        ITenantContext tenant,
        ProviderId? providerId = null,
        string? version = null)
    {
        LogicalKey = !string.IsNullOrWhiteSpace(logicalKey)
            ? logicalKey
            : throw new ArgumentException("logicalKey required.", nameof(logicalKey));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        ContentType = contentType;
        ProviderId = providerId;
        Version = version;
    }
}