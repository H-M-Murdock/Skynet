using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Skynet.Core.i18n;

/// <summary>
/// Ergebnis der Einzel-Template-Validierung.
/// </summary>
public sealed record TemplateValidationResult
{
    /// <summary>
    /// True, wenn das Template syntaktisch korrekt ist.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Liste der gefundenen Fehler (z. B. "Unclosed brace").
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Liste von Warnungen (z. B. "Unbekannter Platzhalter-Typ").
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Extrahierte Platzhalter-Namen (normalisiert), z. B. "OrderId", "User".
    /// </summary>
    public IReadOnlyCollection<string> Placeholders { get; init; } = Array.Empty<string>();

    // --- Factory Methoden ---

    public static TemplateValidationResult Valid(IEnumerable<string> placeholders) 
        => new() 
        { 
            IsValid = true, 
            Placeholders = placeholders.ToImmutableList() 
        };

    public static TemplateValidationResult Invalid(IEnumerable<string> errors) 
        => new() 
        { 
            IsValid = false, 
            Errors = errors.ToImmutableList() 
        };
        
    public static TemplateValidationResult Invalid(params string[] errors) 
        => new() 
        { 
            IsValid = false, 
            Errors = errors.ToImmutableList() 
        };
}