// C#
// Skynet.Core.Materialization/ResourceSerializer.cs
//
// Zweck:
// - Einheitliche, typsichere Basis für Serializer, die Domänenobjekte oder Ressourcentypen in Streams schreiben.
// - Explizite Priorität (MUSS gesetzt werden), damit die Auswahl unter mehreren Serializern deterministisch ist.
// - Expliziter ContentType (MUSS gesetzt werden), der klar definiert, welches Format dieser Serializer erzeugt.
// - Schlanke API: kein sourceType-Parameter; TSource steuert die Wahl.
//
// Ablauf im Materializer (vereinfacht):
// 1) Kandidaten per Priority sortieren (kleiner = früher).
// 2) Für value den ersten Serializer wählen, dessen CanHandle(...) true liefert.
// 3) ContentType stammt aus serializer.ContentType.
// 4) SerializeAsync(...) in den Zielstream schreiben.

using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public interface IResourceSerializer
{
    // MUSS gesetzt werden (kleiner = früher). Zwingt Implementierer zur bewussten Entscheidung.
    int Priority { get; }

    // MUSS gesetzt werden (z. B. "application/json", "application/xml", "application/x-pkcs12").
    string ContentType { get; }

    // Nicht-generischer Eignungscheck (Dispatcher-Ebene). Vermeidet teure Operationen.
    bool CanHandle(object value, string logicalKey, ITenantContext tenant);

    // Nicht-generische Serialisierung in den gegebenen Stream.
    Task SerializeAsync(
        object value,
        string logicalKey,
        ITenantContext tenant,
        Stream destination,
        CancellationToken ct = default);
}

public interface IResourceSerializer<TSource> : IResourceSerializer
{
    // Typsicherer Eignungscheck. Nutzt z. B. Key/Policies/Tenant, kein teures Pre-Serialisieren.
    bool CanHandle(TSource value, string logicalKey, ITenantContext tenant);

    // Typsichere Serialisierung. Schreibt vollständig (Position am Ende). Ownership des Streams liegt beim Aufrufer.
    Task SerializeAsync(
        TSource value,
        string logicalKey,
        ITenantContext tenant,
        Stream destination,
        CancellationToken ct = default);
}

/// <summary>
/// Komfort-Basis mit klaren Pflichten:
/// - Priority: abstract (Implementierer MUSS entscheiden).
/// - ContentType: abstract (Implementierer MUSS festlegen, welches Format erzeugt wird).
/// - Nicht-generische Methoden adaptieren auf die generischen.
/// </summary>
public abstract class ResourceSerializer<TSource> : IResourceSerializer<TSource>
{
    /// <summary>Explizite Auswahl-Priorität (kleiner = früher).</summary>
    public abstract int Priority { get; }

    /// <summary>Vom Serializer produzierter MIME-Typ (z. B. "application/json").</summary>
    public abstract string ContentType { get; }

    /// <summary>Schneller Eignungscheck (keine teure IO/Encoding-Konvertierung).</summary>
    public abstract bool CanHandle(TSource value, string logicalKey, ITenantContext tenant);

    /// <summary>Serialisiert value vollständig in den Zielstream.</summary>
    public abstract Task SerializeAsync(
        TSource value,
        string logicalKey,
        ITenantContext tenant,
        Stream destination,
        CancellationToken ct = default);

    // Adapter: nicht-generischer CanHandle
    public bool CanHandle(object value, string logicalKey, ITenantContext tenant)
        => value is TSource v && CanHandle(v, logicalKey, tenant);

    // Adapter: nicht-generische Serialisierung
    public Task SerializeAsync(
        object value,
        string logicalKey,
        ITenantContext tenant,
        Stream destination,
        CancellationToken ct = default)
        => SerializeAsync((TSource)value, logicalKey, tenant, destination, ct);
}