using Skynet.Core.Bootstrap;
using Skynet.Core.i18n;
using Skynet.Core.Tenant;

namespace Skynet.Tests.i18n;

public class DefaultTenantScopeFormatterTests
{
    private readonly DefaultTenantScopeFormatter _formatter = new();

    [Fact]
    public void ToScope_ShouldReturnSystem_WhenTenantIdIsSystemGuid()
    {
        // Arrange
        var systemId = new TenantId(SystemTenantContext.SystemGuid);

        // Act
        var scope = _formatter.ToScope(systemId);

        // Assert
        Assert.Equal("system", scope);
    }

    [Fact]
    public void ToScope_ShouldReturnSystem_WhenTenantIdIsEmpty()
    {
        // Defensive Fallback Test
        var emptyId = new TenantId(Guid.Empty);
        var scope = _formatter.ToScope(emptyId);
        Assert.Equal("system", scope);
    }

    [Fact]
    public void ToScope_ShouldReturnTenantPrefix_WhenTenantIdIsRegular()
    {
        // Arrange
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var tenantId = new TenantId(guid); 

        // Act
        var scope = _formatter.ToScope(tenantId);

        // Assert
        Assert.Equal($"tenant/{guid}", scope);
    }
}
