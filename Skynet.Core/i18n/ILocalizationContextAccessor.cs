using System.Globalization;
using Microsoft.Extensions.Primitives;
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// Verwaltet die „aktive Kultur“ pro Tenant im aktuellen Ausführungskontext.
/// <para>
/// Aufgaben:
/// - Auflösung der effektiven Kultur (User-Wahl > Browser > Tenant-Default).
/// - Bietet <see cref="IChangeToken"/>, um Caches/UIs bei Kulturwechsel zu benachrichtigen (Reaktivität).
/// </para>
/// </summary>
public interface ILocalizationContextAccessor
{
    /// <summary>
    /// Liefert die aktuell aktive Kultur für den gegebenen Tenant-Kontext.
    /// Wenn keine spezifische Wahl vorliegt, wird auf <see cref="ITenantContext.DefaultCulture"/> zurückgefallen.
    /// </summary>
    CultureInfo GetActiveCulture(ITenantContext tenantContext);

    /// <summary>
    /// Ändert die aktive Kultur zur Laufzeit (z. B. User klickt auf Sprachumschalter).
    /// Feuert das zugehörige Change-Token.
    /// </summary>
    void SetActiveCulture(ITenantContext tenantContext, CultureInfo culture);

    /// <summary>
    /// Liefert die aktuelle Kultur UND ein Token, das bei Änderung gefeuert wird.
    /// Ideal für reaktive UIs (Blazor) oder Caching-Layer.
    /// </summary>
    (CultureInfo culture, IChangeToken token) GetActiveCultureWithWatch(ITenantContext tenantContext);
}
