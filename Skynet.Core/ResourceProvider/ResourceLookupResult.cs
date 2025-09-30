using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public enum ResourceLookupStatus { Found, NotFound, Forbidden, Error, NotModified }

public readonly struct ResourceLookupResult
{
    public ResourceLookupStatus Status { get; }
    public IResourceResult? Resource { get; }
    public string? Reason { get; }             // optional diagnostics

    private ResourceLookupResult(ResourceLookupStatus status, IResourceResult? resource = null,
        string? reason = null)
        => (Status, Resource, Reason) = (status, resource, reason);

    public static ResourceLookupResult Found(IResourceResult r, TenantId? t = null)
        => new(ResourceLookupStatus.Found, r);

    public static ResourceLookupResult NotFound(string? reason = null)
        => new(ResourceLookupStatus.NotFound, reason: reason);

    public static ResourceLookupResult NotModified(string? reason = null)
        => new(ResourceLookupStatus.NotModified, reason: reason);
}