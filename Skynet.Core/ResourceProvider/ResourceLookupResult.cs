using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public enum ResourceLookupStatus { Found, NotFound, Forbidden, Error, NotModified }

public readonly struct ResourceLookupResult
{
    public ResourceLookupStatus Status { get; }
    public IResourceResult? Resource { get; }
    public string? Reason { get; }             // optional diagnostics
    public string? Provider { get; }           // welcher Provider traf?
    public TenantId? ResolvedTenant { get; }   // aus welcher Stufe?

    private ResourceLookupResult(ResourceLookupStatus status, IResourceResult? resource = null,
        string? reason = null, string? provider = null, TenantId? resolvedTenant = null)
        => (Status, Resource, Reason, Provider, ResolvedTenant) = (status, resource, reason, provider, resolvedTenant);

    public static ResourceLookupResult Found(IResourceResult r, string? provider = null, TenantId? t = null)
        => new(ResourceLookupStatus.Found, r, provider: provider, resolvedTenant: t);

    public static ResourceLookupResult NotFound(string? reason = null)
        => new(ResourceLookupStatus.NotFound, reason: reason);

    public static ResourceLookupResult NotModified(string? reason = null)
        => new(ResourceLookupStatus.NotModified, reason: reason);
}