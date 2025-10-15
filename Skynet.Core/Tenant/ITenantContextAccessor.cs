// Skynet.Core.Tenant/ITenantContextAccessor.cs
namespace Skynet.Core.Tenant;

public interface ITenantContextAccessor
{
    ITenantContext? Current { get; set; }

    IDisposable BeginScope(ITenantContext context);
}
