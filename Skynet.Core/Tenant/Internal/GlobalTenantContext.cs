namespace Skynet.Core.Tenant;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

/// <summary>
/// Repräsentiert den globalen System-Kontext (Default Tenant).
/// <para>
/// Dieser Kontext wird verwendet, wenn:
/// - Kein spezifischer Mandant ermittelt werden kann.
/// - Die Anwendung im "System-Modus" startet (z. B. Hintergrund-Jobs, Wartung).
/// </para>
/// Die ID ist <see cref="TenantId.Empty"/> (0000...).
/// </summary>
public sealed class GlobalTenantContext : ITenantContext
{
    public TenantId CurrentTenantId { get; }
    public IReadOnlyList<TenantId> ResolutionChain { get; }
    public string? Name { get; }
    public string? Description { get; }
    public CultureInfo DefaultCulture { get; }
    public TenantId? ParentTenantId { get; }

    /// <summary>
    /// Die Singleton-Instanz des System-Kontexts.
    /// </summary>
    public static GlobalTenantContext Instance { get; } = new GlobalTenantContext();

    private GlobalTenantContext()
    {
        // Nutzung von TenantId.Empty statt einer zufälligen Magic-GUID
        CurrentTenantId = TenantId.Empty;
        
        // System-Default ist oft Invariant oder eine fest definierte "Server-Sprache" (hier de-DE beibehalten)
        DefaultCulture = new CultureInfo("de-DE");
        
        Name = "System";
        Description = "Global system context (Root)";
        ParentTenantId = null;

        // Die Chain enthält nur sich selbst, da System keinen Parent hat.
        ResolutionChain = [CurrentTenantId];
    }
}
