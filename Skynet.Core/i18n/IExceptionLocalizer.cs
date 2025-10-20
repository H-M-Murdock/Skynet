using System.Globalization;
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// Übersetzt Exceptions in nutzer- oder API-taugliche Meldungen.
/// Typisch: Mapping Exception-Typ/Code -> I18NKey + Parameterextraktion (z. B. aus Exception-Daten).
/// Nutzt intern ILocalizationService (Lookup + Render).
/// </summary>
public interface IExceptionLocalizer
{
    /// <summary>
    /// Liefert eine lokalisierte, nutzerfreundliche Nachricht zu einer Exception.
    /// requestedCulture: optional explizite Kultur; null → aus tenantContext.
    /// </summary>
    Task<string> ToUserMessageAsync(
        ITenantContext tenantContext,
        Exception exception,
        CultureInfo? requestedCulture = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Strukturierte Form inkl. Key und Parameter (für Downstream/Telemetry).
    /// </summary>
    Task<LocalizedException> ToLocalizedAsync(
        ITenantContext tenantContext,
        Exception exception,
        CultureInfo? requestedCulture = null,
        CancellationToken cancellationToken = default);
}