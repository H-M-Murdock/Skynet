using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// Lookup-Service, der auf Basis von ITenantContext die Fallback-Suche (Tenant-Chain × Kultur) kapselt.
/// Verantwortlichkeiten:
/// - Ermittelt die Suchreihenfolge aus tenantContext.ResolutionChain und tenantContext.DefaultCulture.
/// - Prüft Varianten via ITemplateRepository.TryGetExactAsync in dieser Reihenfolge.
/// - Keine Render-Logik (die übernimmt ITemplateRenderer).
/// </summary>
public interface ITemplateLookupService
{
    /// <summary>
    /// Sucht ein Template entlang der impliziten Fallback-Kette:
    /// - Tenant-Fallback: tenantContext.ResolutionChain (Current → Parent → System).
    /// - Kultur-Fallback: requestedCulture → requestedCulture.Parent → tenantContext.DefaultCulture → DefaultCulture.Parent → CultureInfo.InvariantCulture.
    /// Die erste gefundene Variante gewinnt.
    /// </summary>
    Task<TemplateLookupResult> FindAsync(
        ITenantContext tenantContext,
        I18NKey key,
        CultureInfo? requestedCulture = null,
        CancellationToken cancellationToken = default);
}