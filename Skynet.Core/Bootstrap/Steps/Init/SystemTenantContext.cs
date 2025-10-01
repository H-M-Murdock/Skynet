// Skynet.Core/Tenant/SystemTenantContext.cs

using System.Globalization;
using Skynet.Core.Tenant;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Fester System-Tenant für Core-Services.
/// - GUID: 85B28EC9-34DB-414B-9E29-2C4AE17A6923
/// - Chain: nur sich selbst (kann später erweitert werden)
/// - Zweck: technische Defaults, Bootstrap-/Systemressourcen
/// </summary>
public sealed class SystemTenantContext : ITenantContext
{
    public static readonly Guid SystemGuid = new("85B28EC9-34DB-414B-9E29-2C4AE17A6923");

    private static readonly TenantId SystemId = new(SystemGuid);
    private static readonly IReadOnlyList<TenantId> Chain = [SystemId];

    public TenantId CurrentTenantId => SystemId;
    public IReadOnlyList<TenantId> ResolutionChain => Chain;

    public string? Name => "System";
    public string? Description => "System tenant (core services and defaults)";
    public CultureInfo DefaultCulture { get; } = new("en-US");
    public TenantId? ParentTenantId => null;
}
