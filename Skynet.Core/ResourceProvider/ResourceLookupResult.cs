using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

// WICHTIG: 0 sollte immer ein "negativer" oder "leerer" Zustand sein, 
// damit default(Struct) keinen validen Treffer vortäuscht.
public enum ResourceLookupStatus 
{ 
    NotFound = 0, // Default
    Found, 
    Forbidden, 
    Error, 
    NotModified 
}

public readonly struct ResourceLookupResult
{
    public ResourceLookupStatus Status { get; }
    public IResourceResult? Resource { get; }
    public string? Reason { get; }

    private ResourceLookupResult(ResourceLookupStatus status, IResourceResult? resource = null, string? reason = null)
        => (Status, Resource, Reason) = (status, resource, reason);

    // TenantId Parameter entfernt (war ungenutzt)
    public static ResourceLookupResult Found(IResourceResult r)
        => new(ResourceLookupStatus.Found, r);

    public static ResourceLookupResult NotFound(string? reason = null)
        => new(ResourceLookupStatus.NotFound, reason: reason);

    public static ResourceLookupResult NotModified(string? reason = null)
        => new(ResourceLookupStatus.NotModified, reason: reason);
        
    // Fehlende Factories ergänzt
    public static ResourceLookupResult Forbidden(string? reason = null)
        => new(ResourceLookupStatus.Forbidden, reason: reason);

    public static ResourceLookupResult Error(string? reason = null)
        => new(ResourceLookupStatus.Error, reason: reason);
        
    // Helper Property
    public bool IsSuccess => Status == ResourceLookupStatus.Found && Resource != null;
}