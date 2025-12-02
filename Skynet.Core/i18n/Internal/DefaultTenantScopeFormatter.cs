namespace Skynet.Core.i18n;

public sealed class DefaultTenantScopeFormatter : ITenantScopeFormatter
{
    public string ToScope(TenantId tenantId) => $"tenant/{tenantId}";
}
