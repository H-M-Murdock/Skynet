using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;

/// <summary>
/// Ergebnis eines Löschvorgangs.
/// </summary>
public interface IResourceDeleteResult
{
    TenantId TenantId { get; }
    string Key { get; }
    bool Deleted { get; }
    /// <summary>Vorherige Version (falls vom Provider ermittelbar).</summary>
    string? PreviousVersion { get; }
    ProviderId? ProviderId { get; }
}