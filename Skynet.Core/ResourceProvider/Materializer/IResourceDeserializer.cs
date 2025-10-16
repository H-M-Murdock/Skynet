// C#
// Skynet.Core.Materialization/ResourceDeserializer.cs
//
// Zweck:
// - Typsichere Deserializer-Basis, die explizit den erwarteten ContentType als Property vorgibt.
// - Priority ist Pflicht (Implementierer MUSS entscheiden), damit die Auswahl deterministisch ist.
// - Schlanke API: der Zieltyp TTarget ist maßgeblich; CanHandle nutzt ExpectedContentType + Key-Heuristiken.
//
// Ablauf im Materializer (vereinfacht):
// 1) Kandidaten nach Priority (kleiner = früher) sortieren.
// 2) Für gewünschten Zieltyp TTarget die Deserializer suchen, deren ExpectedContentType zum Envelope passt.
// 3) Ersten passenden Deserializer wählen und Deserialize(envelope) aufrufen.

namespace Skynet.Core.ResourceProvider;

public interface IResourceDeserializer
{
    // MUSS gesetzt werden (kleiner = früher). Zwingt Implementierer zur bewussten Entscheidung.
    int Priority { get; }

    // Erwarteter MIME-Typ (z. B. "application/json"). Darf breit sein (z. B. "text/*") oder null,
    // wenn der Deserializer selbst sniffen kann. Empfohlen: konkret (deterministische Auswahl).
    string? ExpectedContentType { get; }

    // Nicht-generischer Eignungscheck (Dispatcher-Ebene). Keine teuren IO-Operationen.
    // targetType ist der gewünschte Zieltyp (z. B. typeof(IJsonResource), typeof(ITcpParameters)).
    bool CanHandle(Type targetType, string logicalKey, string? contentType);

    // Nicht-generische Deserialisierung. Liest Stream vollständig (Position am Ende). Kein Dispose hier.
    object? Deserialize(Type targetType, ResourceEnvelope envelope);
}

public interface IResourceDeserializer<out TTarget> : IResourceDeserializer
{
    // Typsicherer Eignungscheck. Nutzt Key/ExpectedContentType/Heuristik, aber keine teuren Operationen.
    bool CanHandle(string logicalKey, string? contentType);

    // Typsichere Deserialisierung.
    TTarget? Deserialize(ResourceEnvelope envelope);
}

/// <summary>
/// Abstrakte Komfortbasis:
/// - Erzwingt Priority und ExpectedContentType (Implementierer MUSS beides festlegen oder sniffen begründen).
/// - Implementiert das nicht-generische Interface per Adapter.
/// </summary>
public abstract class ResourceDeserializer<TTarget> : IResourceDeserializer<TTarget>
{
    /// <summary>Explizite Auswahl-Priorität (kleiner = früher).</summary>
    public abstract int Priority { get; }

    /// <summary>
    /// Erwarteter MIME-Typ. Kann null sein, wenn Sniffing unterstützt wird.
    /// Besser: konkret (z. B. "application/json"), um Selektion zu präzisieren.
    /// </summary>
    public abstract string? ExpectedContentType { get; }

    /// <summary>Schneller Eignungscheck (kein teures Parsing).</summary>
    public abstract bool CanHandle(string logicalKey, string? contentType);

    /// <summary>Deserialisiert in TTarget. Bei Nicht-Erfolg null.</summary>
    public abstract TTarget? Deserialize(ResourceEnvelope envelope);

    // Adapter: nicht-generischer CanHandle
    public bool CanHandle(Type targetType, string logicalKey, string? contentType)
    {
        var targetMatch = targetType == typeof(TTarget) || targetType.IsAssignableFrom(typeof(TTarget));
        if (!targetMatch) return false;

        // Wenn ExpectedContentType angegeben ist, sollte sie zum gelieferten contentType passen.
        if (!string.IsNullOrEmpty(ExpectedContentType) && !string.IsNullOrEmpty(contentType))
        {
            // Einfache Vergleichslogik; bei Bedarf erweitern (Wildcard/StartsWith etc.)
            if (!contentType.Equals(ExpectedContentType, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return CanHandle(logicalKey, contentType);
    }

    // Adapter: nicht-generische Deserialize
    public object? Deserialize(Type targetType, ResourceEnvelope envelope)
        => Deserialize(envelope);
}