using System.Globalization;

namespace Skynet.Core.i18n;

/// <summary>
/// Export/Import-Schnittstelle für Übersetzungen (für externe Übersetzer/Tools).
/// Formate: z. B. CSV, JSON, XLIFF – transportneutral, ohne interne Abhängigkeiten.
/// Verwendung:
/// - Export: ausgewählte Keys und Zielsprachen für einen Tenant-Scope bereitstellen.
/// - Import: zurückgelieferte Übersetzungen einlesen, validieren und speichern (via Repository).
/// </summary>
public interface ITranslationExchangeService
{
    /// <summary>
    /// Exportiert Templates für die angegebenen Keys und Kulturen eines Tenant-Scopes.
    /// - includeExistingOnly: wenn true, nur vorhandene Varianten exportieren; sonst leere Slots für fehlende Varianten erzeugen.
    /// - includeMetadata: wenn true, zusätzlich Info wie Version/LastModified/Source bereitstellen (formatabhängig).
    /// </summary>
    Task<Stream> ExportAsync(
        string tenantScope,
        IReadOnlyCollection<I18NKey> keys,
        IReadOnlyCollection<CultureInfo> cultures,
        TranslationExchangeFormat format,
        bool includeExistingOnly = false,
        bool includeMetadata = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Importiert Übersetzungen in den angegebenen Tenant-Scope.
    /// - options steuert Überschreibregeln, Validierung, Dry-Run.
    /// - Validator (falls registriert) wird genutzt, um Placeholder-Konsistenz zu prüfen.
    /// </summary>
    Task<ImportReport> ImportAsync(
        string tenantScope,
        Stream payload,
        TranslationExchangeFormat format,
        ImportOptions options,
        CancellationToken cancellationToken = default);
}