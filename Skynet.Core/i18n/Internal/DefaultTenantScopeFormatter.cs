using Skynet.Core.Bootstrap; // Falls Zugriff erlaubt, sonst nur Guid nutzen
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// Standard-Implementierung für Tenant-Scopes.
/// Format: 
/// - TenantId == SystemGuid -> "system"
/// - Sonst -> "tenant/{id}"
/// </summary>
public sealed class DefaultTenantScopeFormatter : ITenantScopeFormatter
{
    // Wir nutzen die bekannte System-Guid. 
    // Idealerweise würde man diese via Options/Konstruktor injecten, um Entkopplung zu wahren.
    // Hier nutzen wir die Konstante direkt, da sie im Core definiert ist.
    private static readonly Guid SystemTenantGuid = SystemTenantContext.SystemGuid;

    public string ToScope(TenantId tenantId)
    {
        // Prüfung gegen die System-ID
        if (tenantId.Value == SystemTenantGuid)
        {
            return "system";
        }

        // Fallback für default/empty, falls das doch mal vorkommt (defensive programming)
        if (tenantId.Value == Guid.Empty)
        {
            return "system"; // Oder string.Empty / Fehler, je nach Policy. Hier: Safe Fallback.
        }
        
        return $"tenant/{tenantId}";
    }
}