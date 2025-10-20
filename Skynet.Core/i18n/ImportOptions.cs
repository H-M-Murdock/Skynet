namespace Skynet.Core.i18n;

/// <summary>
/// Optionen für den Importvorgang.
/// </summary>
public sealed class ImportOptions
{
    /// <summary>Bestehende Varianten überschreiben (true) oder bestehen lassen (false).</summary>
    public bool OverwriteExisting { get; init; } = false;

    /// <summary>Platzhalter-Konsistenz prüfen und bei Fehler abbrechen.</summary>
    public bool ValidatePlaceholders { get; init; } = true;

    /// <summary>
    /// Strikte Kulturprüfung: nur erlaubte/konfigurierte Kulturen akzeptieren.
    /// Wenn false, unbekannte Kulturen als Warnung statt Fehler behandeln.
    /// </summary>
    public bool StrictCultures { get; init; } = true;

    /// <summary>Dry-Run: nur prüfen, nichts persistieren.</summary>
    public bool DryRun { get; init; } = false;
}