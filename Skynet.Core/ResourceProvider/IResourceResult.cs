using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Read-only Vertrag für eine aufgelöste Ressource (Inhalt + Metadaten).
/// Repräsentiert das *Ergebnis des Findens* einer Ressource, nicht das Domänenobjekt selbst.
/// Der Consumer ist verantwortlich für das Disposen (besitzt den Stream).
/// </summary>
public interface IResourceResult : IDisposable, IAsyncDisposable
{
    /// <summary>Der Tenant, für den die Ressource gefunden wurde (kann vom Request abweichen, z.B. Fallback).</summary>
    TenantId TenantId { get; }

    /// <summary>Der logische Schlüssel der Ressource.</summary>
    string Key { get; }

    /// <summary>
    /// Der Inhalt als Stream. Position sollte idealerweise auf 0 stehen.
    /// </summary>
    Stream Content { get; }

    /// <summary>MIME-Type (z.B. "application/json"). Kann null sein, wenn unbekannt.</summary>
    string? ContentType { get; }

    /// <summary>Zeitstempel der letzten Änderung (UTC).</summary>
    DateTimeOffset? LastModified { get; }

    /// <summary>Größe des Inhalts in Bytes (falls bekannt).</summary>
    long? ContentLength { get; }

    /// <summary>
    /// Opaques Versionstoken (ETag/Hash). Ändert sich, wenn sich der Inhalt ändert.
    /// Dient dem optimistischen Locking und Caching.
    /// </summary>
    string? Version { get; }

    /// <summary>
    /// Identität des Providers, der dieses Ergebnis geliefert hat (für Logging/Tracing/Cache).
    /// </summary>
    ProviderId? ProviderId { get; }
}