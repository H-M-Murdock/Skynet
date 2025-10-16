// Skynet.Core.Materialization/ResourceConverter.cs
//
// Zweck dieses Moduls:
// - Einheitliche Verträge für Ressourcenkonvertierung definieren.
// - Typsichere (generische) Converter auf Basis von IRawResource ermöglichen,
//   gleichzeitig aber eine homogene Registry über ein nicht-generisches Interface bereitstellen.
// - Prioritäten unterstützen, damit bei mehreren Kandidaten deterministisch der “beste” gewählt wird.
//
// Einsatz im Datenfluss (vereinfacht):
// 1) Deserializer liest Stream -> erzeugt eine (typisierte) Resource, z. B. IJsonResource, IXmlResource oder IRawResource.
// 2) Converter wandelt diese Resource in Domänentypen um, z. B. ITcpParameters, ICertificateEntity.
// 3) Materializer orchestriert: Wählt passenden Converter nach Priority, Key, Tenant, Zieltyp.
//
// Designhinweise:
// - Alle Converter sind tenant-aware (Policies, Namensräume).
// - CanConvert soll schnell und ohne teure IO/Parsing-Operationen entscheiden.
// - Convert soll bei “nicht passend” null zurückgeben (Try-Pattern), nicht werfen.
// - Priority: kleiner = früher (höhere Präferenz), z. B. projektspezifischer Converter (100) vor generischem (1000).

using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Nicht-generisches Dispatch-Interface für Converter.
/// Dient als gemeinsame Obermenge, sodass alle Converter in einer Registry gehalten
/// und vom Materializer einheitlich angesprochen werden können.
/// </summary>
public interface IResourceConverter
{
    /// <summary>
    /// Auswahl-Priorität (kleiner = früher). Dient der stabilen Reihenfolge bei mehreren Kandidaten.
    /// Beispiel: Ein projektspezifischer Converter kann mit Priority=100 eine generische Variante (1000) übersteuern.
    /// </summary>
    int Priority => 1000;

    /// <summary>
    /// Prüft, ob eine Konvertierung vom Quell- zum Zieltyp für den angegebenen Key/Tenant sinnvoll möglich ist.
    /// </summary>
    /// <param name="sourceType">Typ der Eingangsinstanz (z. B. IJsonResource, IXmlResource, IRawResource).</param>
    /// <param name="targetType">Zieltyp (z. B. ITcpParameters, ICertificateEntity, IJsonResource).</param>
    /// <param name="logicalKey">Logischer Schlüssel (Domain-/Routing-Kontext; tenant-neutral, Tenant separat).</param>
    /// <param name="tenant">Aktueller Tenant-Kontext (Policies, Namespaces, Fallbackregeln).</param>
    /// <returns>true, wenn dieser Converter geeignet ist.</returns>
    bool CanConvert(Type sourceType, Type targetType, string logicalKey, ITenantContext tenant);

    /// <summary>
    /// Führt die Konvertierung durch. Bei Nicht-Erfolg null zurückgeben (Try-Pattern).
    /// Implementierungen dürfen den Inhalt der Quellinstanz lesen, aber nicht deren Lebenszyklus (Dispose) übernehmen.
    /// </summary>
    /// <param name="source">Quellinstanz (kompatibel zu <paramref name="sourceType"/>).</param>
    /// <param name="sourceType">Quelltyp (zur Validierung/Dispatch).</param>
    /// <param name="targetType">Zieltyp (konkreter gewünschter Typ).</param>
    /// <param name="logicalKey">Logischer Schlüssel (Kontext für Mappingregeln).</param>
    /// <param name="tenant">Aktueller Tenant-Kontext.</param>
    /// <returns>Konvertierte Instanz oder null, falls nicht umsetzbar.</returns>
    object? Convert(object source, Type sourceType, Type targetType, string logicalKey, ITenantContext tenant);
}

/// <summary>
/// Typsicheres, generisches Converter-Interface auf Basis von IRawResource.
/// Implementierungen können so direkt mit einem konkreten Quell-Resource-Typ arbeiten
/// (z. B. IJsonResource, IXmlResource, IRawResource) und eine definierte Zielklasse liefern.
/// </summary>
/// <typeparam name="TSource">Quelltyp, muss IRawResource (oder abgeleitet) sein.</typeparam>
/// <typeparam name="TTarget">Zieltyp (Domänentyp oder ebenfalls Resource-Typ).</typeparam>
public interface IResourceConverter<TSource, TTarget> : IResourceConverter
    where TSource : IRawResource
{
    /// <summary>
    /// Prüft, ob eine Konvertierung von <typeparamref name="TSource"/> zu <typeparamref name="TTarget"/>
    /// für den angegebenen Key/Tenant sinnvoll möglich ist.
    /// </summary>
    /// <param name="targetType">Erwarteter Zieltyp (normalerweise typeof(TTarget)).</param>
    /// <param name="logicalKey">Logischer Schlüssel (Domain-/Routing-Kontext).</param>
    /// <param name="tenant">Aktueller Tenant-Kontext.</param>
    /// <returns>true, wenn dieser Converter geeignet ist.</returns>
    bool CanConvert(Type targetType, string logicalKey, ITenantContext tenant);

    /// <summary>
    /// Führt die Konvertierung durch. Bei Nicht-Erfolg null zurückgeben (Try-Pattern).
    /// </summary>
    /// <param name="source">Quellinstanz vom Typ <typeparamref name="TSource"/>.</param>
    /// <param name="targetType">Zieltyp (normalerweise typeof(TTarget)).</param>
    /// <param name="logicalKey">Logischer Schlüssel.</param>
    /// <param name="tenant">Aktueller Tenant-Kontext.</param>
    /// <returns>Instanz von <typeparamref name="TTarget"/> oder null, falls nicht umsetzbar.</returns>
    TTarget? Convert(TSource source, Type targetType, string logicalKey, ITenantContext tenant);
}

/// <summary>
/// Komfort-Basis für generische Converter. Implementiert das nicht-generische Dispatch-Interface
/// und delegiert auf die typsicheren Methoden.
/// </summary>
/// <typeparam name="TSource">Quelltyp (IRawResource-basiert).</typeparam>
/// <typeparam name="TTarget">Zieltyp.</typeparam>
public abstract class ResourceConverter<TSource, TTarget> : IResourceConverter<TSource, TTarget>
    where TSource : IRawResource
{
    /// <inheritdoc />
    public abstract int Priority { get; }

    /// <inheritdoc />
    public abstract bool CanConvert(Type targetType, string logicalKey, ITenantContext tenant);

    /// <inheritdoc />
    public abstract TTarget? Convert(TSource source, Type targetType, string logicalKey, ITenantContext tenant);

    bool IResourceConverter.CanConvert(Type sourceType, Type targetType, string logicalKey, ITenantContext tenant)
        => typeof(TSource).IsAssignableFrom(sourceType) && CanConvert(targetType, logicalKey, tenant);

    object? IResourceConverter.Convert(object source, Type sourceType, Type targetType, string logicalKey, ITenantContext tenant)
        => Convert((TSource)source, targetType, logicalKey, tenant);
}

