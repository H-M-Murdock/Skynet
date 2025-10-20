// Skynet.Core/ResourceProvider/IResourceWriter.cs
namespace Skynet.Core.ResourceProvider;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Schreib-/Mutationsschnittstelle für Ressourcen (ergänzt den read-only Locator).
/// Unterstützt Erstellen/Aktualisieren/Löschen mit optimistischer Nebenläufigkeit (ETag).
/// Implementierungen dürfen die Semantik pro ResourceKind einschränken.
/// </summary>
public interface IResourceWriter
{
    /// <summary>
    /// Erstellt oder aktualisiert eine Ressource.
    /// - createIfMissing: true => Ressource wird neu angelegt, falls nicht vorhanden.
    /// - ifMatch: ETag, das übereinstimmen muss (optimistisches Locking) – null = keine Bedingung.
    /// - contentType: optionaler MIME-Typ (z. B. "application/json").
    /// </summary>
    /// <returns>Metadaten des Schreibvorgangs (z. B. neues ETag/LastModified).</returns>
    Task<IResourceWriteResult> WriteAsync(
        ResourceRequest request,
        Stream content,
        bool createIfMissing = true,
        string? ifMatch = null,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Löscht eine Ressource.
    /// - ifMatch: ETag, das übereinstimmen muss (optimistisches Locking) – null = keine Bedingung.
    /// </summary>
    Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optionale Fähigkeitenbeschreibung für Writer zur konsistenten Auswahl.
/// Implementierungen können signalisieren, welche Requests sie schreiben können.
/// </summary>
public interface IResourceWriteCapabilities
{
    /// <summary>
    /// Schneller Check, ob dieser Writer den Request grundsätzlich bedienen kann
    /// (z. B. abhängig von ResourceKind, Key-Prefix, Policies).
    /// </summary>
    bool CanHandle(ResourceRequest request);

    /// <summary>
    /// Optional: Priorität für Schreibauswahl (kleiner = bevorzugt).
    /// </summary>
    int Priority => 1000;
}

