namespace Skynet.Core.Tenant;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

/// <summary>
/// Einfache konkrete ITenantContext-Implementierung für Programm-/Default-Tenants.
/// - Repräsentiert einen aktuellen Tenant inkl. optionalem Parent und abgeleiteter Fallback-Chain.
/// - Geeignet für Apps/Tests, in denen die Tenant-Hierarchie statisch oder vorab bekannt ist.
/// </summary>
public sealed class ProgramTenantContext : ITenantContext
{
    public TenantId CurrentTenantId { get; }
    public IReadOnlyList<TenantId> ResolutionChain { get; }
    public string? Name { get; }
    public string? Description { get; }
    public CultureInfo DefaultCulture { get; }
    public TenantId? ParentTenantId { get; }

    /// <summary>
    /// Globale, vorinitialisierte Singleton-Instanz für den typischen Programm-/Default-Tenant.
    /// </summary>
    public static ProgramTenantContext Instance { get; } = new ProgramTenantContext();

    /// <summary>
    /// Privater parameterloser Konstruktor, der alle Properties mit sinnvollen Standardwerten belegt.
    /// </summary>
    private ProgramTenantContext()
    {
        // Sinnvolle Defaults:
        // - Aktueller Tenant: "program" (oder eine feste GUID, falls TenantId das unterstützt)
        // - DefaultCulture: "en-US"
        // - Name/Description: sprechende Defaults
        // - ParentTenantId: none
        // - ResolutionChain: [CurrentTenantId]
        CurrentTenantId = new TenantId(new Guid("85B28EC9-34DB-414B-9E29-2C4AE17A6923"));
        DefaultCulture = new CultureInfo("de-DE");
        Name = "System";
        Description = "Program default tenant context";
        ParentTenantId = null;

        var chain = ImmutableArray.CreateBuilder<TenantId>();
        chain.Add(CurrentTenantId);
        ResolutionChain = chain.ToImmutable();
    }

}
