using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// Übersetzt domänenspezifische Tenant-Identitäten in einen stabilen tenantScope-String,
/// der vom Repository/Storage verwendet wird (z. B. "tenant/{id}" oder "system").
/// Ziel: Entkopplung zwischen Tenant-Modell und Storage-Schlüsseln.
/// </summary>
public interface ITenantScopeFormatter
{
    /// <summary>
    /// Gibt den kanonischen Scope-String für einen Tenant zurück.
    /// Muss deterministisch und stabil sein (für Export/Import/Audit).
    /// </summary>
    string ToScope(TenantId tenantId);
}
