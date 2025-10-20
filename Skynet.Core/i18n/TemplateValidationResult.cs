namespace Skynet.Core.i18n;

/// <summary>
/// Ergebnis der Einzel-Template-Validierung.
/// </summary>
public sealed class TemplateValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = new List<string>();
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();

    /// <summary>Extrahierte Platzhalter-Namen (normalisiert).</summary>
    public IReadOnlyCollection<string> Placeholders { get; init; } = new List<string>();
}