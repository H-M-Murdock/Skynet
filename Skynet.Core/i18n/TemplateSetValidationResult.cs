using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Skynet.Core.i18n;

/// <summary>
/// Ergebnis der Varianten-Konsistenzprüfung.
/// </summary>
public sealed record TemplateSetValidationResult
{
    /// <summary>
    /// True, wenn keine Fehler gefunden wurden.
    /// </summary>
    public bool IsConsistent { get; init; }

    /// <summary>
    /// Globale Fehler, die das ganze Set betreffen (z. B. "Inkonsistente Platzhalter").
    /// </summary>
    public IReadOnlyList<string> Errors { get;init; } = Array.Empty<string>();

    /// <summary>
    /// Detaillierte Fehler pro Kultur (Key = Culture Code, Value = Liste der Fehler).
    /// Hilft UI/Tools, den Fehler direkt beim richtigen Feld anzuzeigen.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ErrorsByCulture { get; init; } 
        = new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>());

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Vereinigte Menge aller in allen Varianten gefundenen Platzhalter.
    /// </summary>
    public IReadOnlyCollection<string> AllPlaceholders { get; init; } = Array.Empty<string>();

    // --- Factory Methoden für komfortable Nutzung ---

    public static TemplateSetValidationResult Success(IEnumerable<string> allPlaceholders) 
        => new() 
        { 
            IsConsistent = true, 
            AllPlaceholders = new List<string>(allPlaceholders).AsReadOnly() 
        };

    public static TemplateSetValidationResult Failure(
        IEnumerable<string> globalErrors, 
        IDictionary<string, List<string>>? cultureErrors = null)
    {
        // Umwandlung in ReadOnly-Strukturen für Sicherheit
        var dict = new Dictionary<string, IReadOnlyList<string>>();
        if (cultureErrors != null)
        {
            foreach (var kvp in cultureErrors)
            {
                dict[kvp.Key] = kvp.Value.AsReadOnly();
            }
        }

        return new()
        {
            IsConsistent = false,
            Errors = new List<string>(globalErrors).AsReadOnly(),
            ErrorsByCulture = new ReadOnlyDictionary<string, IReadOnlyList<string>>(dict)
        };
    }
}
