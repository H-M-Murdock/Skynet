using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Ergebnis eines Löschvorgangs.
/// </summary>
public interface IResourceDeleteResult
{
    TenantId TenantId { get; }
    string Key { get; }
    
    /// <summary>
    /// True, wenn die Ressource tatsächlich gelöscht wurde. 
    /// False, wenn sie gar nicht existierte (idempotent).
    /// </summary>
    bool Deleted { get; }
    
    /// <summary>Vorherige Version (falls vom Provider ermittelbar/unterstützt).</summary>
    string? PreviousVersion { get; }
    
    ProviderId? ProviderId { get; }
}

/// <summary>
/// Standard-Implementierung für IResourceDeleteResult.
/// Erspart den Providern das Schreiben eigener DTO-Klassen.
/// </summary>
public sealed class ResourceDeleteResult : IResourceDeleteResult
{
    public TenantId TenantId { get; }
    public string Key { get; }
    public bool Deleted { get; }
    public string? PreviousVersion { get; }
    public ProviderId? ProviderId { get; }

    public ResourceDeleteResult(
        TenantId tenantId, 
        string key, 
        bool deleted, 
        string? previousVersion = null, 
        ProviderId? providerId = null)
    {
        TenantId = tenantId;
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Deleted = deleted;
        PreviousVersion = previousVersion;
        ProviderId = providerId;
    }
    
    public override string ToString()
        => $"ResourceDeleteResult {{ Key={Key}, Deleted={Deleted}, Provider={ProviderId?.ToString() ?? "N/A"} }}";
}