using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// High-Level-API: Findet Templates (mit Fallback über ITenantContext) und rendert sie mit Parametern.
/// Nutzt intern ITemplateLookupService + ITemplateRenderer.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Rendert den Text zum Key unter Berücksichtigung der Tenant-Fallback-Kette und Kultur-Fallbacks.
    /// requestedCulture: optional explizite Kultur; null → tenantContext.DefaultCulture-basierte Kette.
    /// </summary>
    Task<string> FormatAsync(
        ITenantContext tenantContext,
        I18NKey key,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CultureInfo? requestedCulture = null,
        CancellationToken cancellationToken = default);
}
