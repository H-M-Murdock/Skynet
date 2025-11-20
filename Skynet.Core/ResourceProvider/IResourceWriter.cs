// Skynet.Core/ResourceProvider/IResourceWriter.cs
namespace Skynet.Core.ResourceProvider;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Schreib-/Mutationsschnittstelle für Ressourcen (ergänzt den read-only Locator).
/// Unterstützt Erstellen/Aktualisieren/Löschen mit optimistischer Nebenläufigkeit (ETag).
/// Implementierungen dürfen die Semantik pro ResourceKind einschränken (z.B. nur Configs schreiben).
/// </summary>
public interface IResourceWriter
{
    /// <summary>
    /// Erstellt oder aktualisiert eine Ressource.
    /// </summary>
    /// <param name="request">Ziel-Key und Tenant.</param>
    /// <param name="content">Der zu schreibende Inhalt (Stream).</param>
    /// <param name="createIfMissing">True => Ressource wird neu angelegt, falls nicht vorhanden. False => Fehler, wenn nicht vorhanden.</param>
    /// <param name="ifMatch">Erwartetes ETag für optimistisches Locking. Bei Mismatch wird eine Exception geworfen (z.B. IOException/PreconditionFailed). Null = Überschreiben erzwingen.</param>
    /// <param name="contentType">Optionaler MIME-Typ (z. B. "application/json").</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
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
    /// </summary>
    /// <param name="request">Ziel-Key und Tenant.</param>
    /// <param name="ifMatch">Erwartetes ETag. Bei Mismatch wird nicht gelöscht und Fehler geworfen. Null = bedingungslos löschen.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optionale Fähigkeitenbeschreibung für Writer zur konsistenten Auswahl.
/// Implementierungen können signalisieren, welche Requests sie schreiben können.
/// Writer, die dieses Interface NICHT implementieren, werden als "Fallback" mit niedrigster Priorität behandelt.
/// </summary>
public interface IResourceWriteCapabilities
{
    /// <summary>
    /// Schneller Check, ob dieser Writer den Request grundsätzlich bedienen kann
    /// (z. B. abhängig von ResourceKind, Key-Prefix, Policies).
    /// </summary>
    bool CanHandle(ResourceRequest request);

    /// <summary>
    /// Priorität für die Schreibauswahl (kleiner = bevorzugt).
    /// Default ist 1000 (niedrige Priorität).
    /// </summary>
    int Priority => 1000;
}