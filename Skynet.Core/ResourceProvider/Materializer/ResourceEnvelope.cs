using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Übergabeobjekt (DTO) für Deserializer.
/// Kapselt den rohen Stream und alle Kontextinformationen, die für das Parsing nötig sein könnten.
/// Wird vom Materializer erstellt und an die Deserializer übergeben.
/// </summary>
public sealed class ResourceEnvelope
{
    /// <summary>Logischer Schlüssel der Ressource (ohne Tenant-Präfix).</summary>
    public string LogicalKey { get; }

    /// <summary>MIME-Type laut Provider (kann null/ungenau sein).</summary>
    public string? ContentType { get; }

    /// <summary>
    /// Der offene Datenstrom. Position ist typischerweise am Anfang.
    /// Der Stream gehört NICHT dem Envelope (sondern dem ResourceResult/Materializer), 
    /// daher implementiert diese Klasse kein IDisposable.
    /// </summary>
    public Stream Content { get; }

    /// <summary>Der Tenant-Kontext, in dem die Ressource gefunden wurde.</summary>
    public ITenantContext Tenant { get; }

    /// <summary>ID des Providers (für Tracing/Debugging).</summary>
    public ProviderId? ProviderId { get; }

    /// <summary>Version/ETag der Quelle (für Caching-Entscheidungen im Deserializer).</summary>
    public string? Version { get; }

    public ResourceEnvelope(
        string logicalKey,
        string? contentType,
        Stream content,
        ITenantContext tenant,
        ProviderId? providerId = null,
        string? version = null)
    {
        if (string.IsNullOrWhiteSpace(logicalKey))
            throw new ArgumentException("Logical key is required.", nameof(logicalKey));

        LogicalKey = logicalKey;
        ContentType = contentType;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        ProviderId = providerId;
        Version = version;
    }

    public override string ToString()
        => $"ResourceEnvelope {{ Key={LogicalKey}, Type={ContentType ?? "?"}, Size={TryGetLength()}, Tenant={Tenant} }}";

    private string TryGetLength()
    {
        try { return Content.Length.ToString(); } catch { return "?"; }
    }
}