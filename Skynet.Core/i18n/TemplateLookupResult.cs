using System.Globalization;

namespace Skynet.Core.i18n;

/// <summary>
/// Ergebnis eines Template-Lookups mit Debug-Infos für Telemetrie/Diagnose.
/// </summary>
public sealed record TemplateLookupResult
{
    /// <summary>
    /// Gibt an, ob ein Template gefunden wurde.
    /// </summary>
    public bool IsFound => Template != null;

    /// <summary>
    /// Das gefundene Template (oder null).
    /// </summary>
    public I18NTemplate? Template { get; init; }

    /// <summary>
    /// Die tatsächlich verwendete Kultur (kann von requestedCulture abweichen, z.B. Fallback auf Parent).
    /// </summary>
    public CultureInfo ResolvedCulture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Menschlich lesbare Darstellung der abgearbeiteten Kandidaten.
    /// Format: "tenant/123:de-CH -> tenant/123:de -> tenant/parent:de -> system:de"
    /// </summary>
    public string ResolutionPath { get; init; } = string.Empty;

    /// <summary>
    /// Erzeugt ein erfolgreiches Ergebnis.
    /// </summary>
    public static TemplateLookupResult Found(I18NTemplate template, CultureInfo culture) 
        => new() { Template = template, ResolvedCulture = culture };

    /// <summary>
    /// Erzeugt ein negatives Ergebnis (nicht gefunden).
    /// </summary>
    public static TemplateLookupResult NotFound(I18NKey key, CultureInfo requested) 
        => new() { Template = null, ResolvedCulture = requested, ResolutionPath = $"Key '{key}' not found for culture '{requested.Name}'" };
}