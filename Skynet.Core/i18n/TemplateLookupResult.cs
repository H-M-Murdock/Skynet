using System.Globalization;

namespace Skynet.Core.i18n;

/// <summary>
/// Ergebnis eines Template-Lookups mit Debug-Infos für Telemetrie/Diagnose.
/// </summary>
public sealed class TemplateLookupResult
{
    public I18NTemplate? Template { get; init; }

    /// <summary>Die tatsächlich verwendete Kultur (kann von requestedCulture abweichen).</summary>
    public CultureInfo? ResolvedCulture { get; init; }

    /// <summary>
    /// Menschlich lesbare Darstellung der abgearbeiteten Kandidaten, z. B.:
    /// "tenant/123:de-CH -> tenant/123:de -> tenant/parent:de -> system:de"
    /// </summary>
    public string ResolutionPath { get; init; } = string.Empty;
}