using System.Globalization;
using Microsoft.Extensions.Primitives;
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// Verwaltet die „aktive Kultur“ pro Tenant (z. B. aus User-/UI-Auswahl).
/// Bietet Change-Token, um Caches/UIs bei Kulturwechsel zu invalidieren.
/// </summary>
public interface ILocalizationContextAccessor
{
    /// <summary>Aktive Kultur des Tenants (fällt ggf. auf tenant.DefaultCulture zurück).</summary>
    CultureInfo GetActiveCulture(ITenantContext tenantContext);

    /// <summary>Setzt/ändert die aktive Kultur und triggert Change-Token.</summary>
    void SetActiveCulture(ITenantContext tenantContext, CultureInfo culture);

    /// <summary>Kombiniert Abfrage mit Watch-Mechanismus für Reaktivität.</summary>
    (CultureInfo culture, IChangeToken token) GetActiveCultureWithWatch(ITenantContext tenantContext);
}
