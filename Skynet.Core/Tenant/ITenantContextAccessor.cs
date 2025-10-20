// Skynet.Core.Tenant/ITenantContextAccessor.cs
namespace Skynet.Core.Tenant;

public interface ITenantContextAccessor
{
    ITenantContext? Current { get; set; }

    /// <summary>
    /// Establishes a scope with the given tenant context and restores the previous value on dispose.
    /// Safe for nesting and async flows (AsyncLocal-backed).
    /// </summary>
    IDisposable BeginScope(ITenantContext context);
}
