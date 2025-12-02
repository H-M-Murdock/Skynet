using Skynet.Core.Tenant;

namespace Skynet.Tests.tenant;

public class GlobalTenantContextTests
{
    [Fact]
    public void Instance_Should_Be_Singleton_And_NotNull()
    {
        var instance1 = GlobalTenantContext.Instance;
        var instance2 = GlobalTenantContext.Instance;

        Assert.NotNull(instance1);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void CurrentTenantId_Should_Be_Empty()
    {
        var context = GlobalTenantContext.Instance;
        
        Assert.Equal(TenantId.Empty, context.CurrentTenantId);
    }

    [Fact]
    public void ResolutionChain_Should_Contain_Only_EmptyId()
    {
        var context = GlobalTenantContext.Instance;

        Assert.Single(context.ResolutionChain);
        Assert.Equal(TenantId.Empty, context.ResolutionChain.First());
    }

    [Fact]
    public void ParentTenantId_Should_Be_Null()
    {
        Assert.Null(GlobalTenantContext.Instance.ParentTenantId);
    }
}
