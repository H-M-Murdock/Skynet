using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// High-Level-API für die Lokalisierung.
/// Orchestriert die Suche nach Templates (Lookup) und deren Aufbereitung (Rendering).
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Formatiert einen Textbaustein für einen spezifischen Tenant und eine Kultur.
    /// </summary>
    /// <param name="tenantContext">Der Kontext, in dem gesucht wird (bestimmt die ResolutionChain).</param>
    /// <param name="key">Der logische Schlüssel des Textes.</param>
    /// <param name="parameters">Optionale Parameter für Platzhalter im Template.</param>
    /// <param name="requestedCulture">
    /// Die gewünschte Zielkultur. 
    /// Wenn null, wird <see cref="ITenantContext.DefaultCulture"/> oder <see cref="CultureInfo.CurrentUICulture"/> verwendet.
    /// </param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>
    /// Den formatierten String. 
    /// Falls kein Template gefunden wird, wird eine String-Repräsentation des Keys zurückgegeben (Graceful Degrade).
    /// </returns>
    Task<string> FormatAsync(
        ITenantContext tenantContext,
        I18NKey key,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CultureInfo? requestedCulture = null,
        CancellationToken cancellationToken = default);
}
