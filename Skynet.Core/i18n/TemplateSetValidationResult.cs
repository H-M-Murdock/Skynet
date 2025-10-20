using System.Collections.Generic;

namespace Skynet.Core.i18n;

/// <summary>
/// Ergebnis der Varianten-Konsistenzprüfung.
/// </summary>
public sealed class TemplateSetValidationResult
{
    public bool IsConsistent { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = new List<string>();
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();

    /// <summary>Vereinigte Menge aller in allen Varianten gefundenen Platzhalter.</summary>
    public IReadOnlyCollection<string> AllPlaceholders { get; init; } = new List<string>();
}
