using Moq;
using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;

namespace Skynet.Tests.ResourceProvider;

public class ResourceLocatorTests
{
    private readonly Mock<ITenantContext> _tenantContextMock;
    
    private readonly TenantId _tenantA = TenantId.New();
    private readonly TenantId _tenantGlobal = TenantId.New();

    public ResourceLocatorTests()
    {
        _tenantContextMock = new Mock<ITenantContext>();
        // Standard-Chain: TenantA -> Global
        _tenantContextMock.Setup(x => x.ResolutionChain)
            .Returns(new[] { _tenantA, _tenantGlobal });
    }

    [Fact]
    public async Task TryGetAsync_Should_Respect_Provider_Priority()
    {
        // Arrange
        var req = new ResourceRequest(_tenantA, "config.json", ResourceKind.Config);

        // Provider 1 (High Priority / Low Int) - Sagt "NotFound"
        var p1 = new Mock<IResourceReader>();
        p1.Setup(x => x.Priority).Returns(10);
        p1.Setup(x => x.CanHandle(It.IsAny<ResourceRequest>())).Returns(true);
        p1.Setup(x => x.TryGetAsync(It.IsAny<ResourceRequest>(), default))
          .ReturnsAsync(ResourceLookupResult.NotFound());

        // Provider 2 (Low Priority / High Int) - Sagt "Found"
        var p2 = new Mock<IResourceReader>();
        p2.Setup(x => x.Priority).Returns(20);
        p2.Setup(x => x.CanHandle(It.IsAny<ResourceRequest>())).Returns(true);
        
        var expectedResult = new Mock<IResourceResult>();
        p2.Setup(x => x.TryGetAsync(It.IsAny<ResourceRequest>(), default))
          .ReturnsAsync(ResourceLookupResult.Found(expectedResult.Object));

        var locator = new ResourceLocator(new[] { p2.Object, p1.Object }, null, _tenantContextMock.Object);

        // Act
        var result = await locator.TryGetAsync(req);

        // Assert
        Assert.Equal(ResourceLookupStatus.Found, result.Status);
        Assert.Same(expectedResult.Object, result.Resource);
        
        // Sicherstellen, dass P1 ZUERST gefragt wurde (wegen Priority 10 vs 20)
        p1.Verify(x => x.TryGetAsync(It.IsAny<ResourceRequest>(), default), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TryGetAsync_Should_Traverse_Tenant_Chain()
    {
        // Arrange
        var req = new ResourceRequest(_tenantA, "global-settings", ResourceKind.Config);

        var provider = new Mock<IResourceReader>();
        provider.Setup(x => x.CanHandle(It.IsAny<ResourceRequest>())).Returns(true);
        provider.Setup(x => x.Priority).Returns(1);

        // 1. Aufruf mit TenantA -> NotFound
        provider.Setup(x => x.TryGetAsync(It.Is<ResourceRequest>(r => r.TenantId == _tenantA), default))
            .ReturnsAsync(ResourceLookupResult.NotFound());

        // 2. Aufruf mit Global -> Found
        var expectedResult = new Mock<IResourceResult>();
        provider.Setup(x => x.TryGetAsync(It.Is<ResourceRequest>(r => r.TenantId == _tenantGlobal), default))
            .ReturnsAsync(ResourceLookupResult.Found(expectedResult.Object));

        var locator = new ResourceLocator(new[] { provider.Object }, null, _tenantContextMock.Object);

        // Act
        var result = await locator.TryGetAsync(req);

        // Assert
        Assert.Equal(ResourceLookupStatus.Found, result.Status);
        // Prüfen, ob beide Tenants nacheinander abgefragt wurden
        provider.Verify(x => x.TryGetAsync(It.Is<ResourceRequest>(r => r.TenantId == _tenantA), default), Times.Once);
        provider.Verify(x => x.TryGetAsync(It.Is<ResourceRequest>(r => r.TenantId == _tenantGlobal), default), Times.Once);
    }

    [Fact]
    public async Task ListKeysAsync_Should_Merge_And_Deduplicate_Keys()
    {
        // Arrange
        var req = new ResourceRequest(_tenantA, "", ResourceKind.Config);

        // Provider A: hat "a.json" und "common.json"
        var p1 = new Mock<IResourceReader>();
        p1.Setup(x => x.CanHandle(It.IsAny<ResourceRequest>())).Returns(true);
        p1.Setup(x => x.ListKeysAsync(It.IsAny<ResourceRequest>(), null, null, default))
          .ReturnsAsync((new[] { "a.json", "common.json" }, null));

        // Provider B: hat "b.json" und "common.json"
        var p2 = new Mock<IResourceReader>();
        p2.Setup(x => x.CanHandle(It.IsAny<ResourceRequest>())).Returns(true);
        p2.Setup(x => x.ListKeysAsync(It.IsAny<ResourceRequest>(), null, null, default))
          .ReturnsAsync((new[] { "b.json", "common.json" }, null));

        var locator = new ResourceLocator(new[] { p1.Object, p2.Object }, null, _tenantContextMock.Object);

        // Act
        var result = await locator.ListKeysAsync(req);

        // Assert
        // "common.json" darf nur 1x vorkommen (Set-Logik)
        // Alphabetisch sortiert
        Assert.Equal(3, result.Keys.Count);
        Assert.Equal(new[] { "a.json", "b.json", "common.json" }, result.Keys);
    }

    [Fact]
    public async Task WriteAsync_Should_Select_Writer_By_Capabilities()
    {
        // Arrange
        var req = new ResourceRequest(_tenantA, "out.pdf", ResourceKind.Asset);
        var stream = new MemoryStream();

        // Writer 1: Kann Assets NICHT schreiben
        var w1 = new Mock<IResourceWriter>();
        w1.As<IResourceWriteCapabilities>().Setup(x => x.CanHandle(req)).Returns(false);

        // Writer 2: Kann Assets schreiben
        var w2 = new Mock<IResourceWriter>();
        w2.As<IResourceWriteCapabilities>().Setup(x => x.CanHandle(req)).Returns(true);
        w2.Setup(x => x.WriteAsync(It.IsAny<ResourceRequest>(), It.IsAny<Stream>(), true, null, null, default))
          .ReturnsAsync(Mock.Of<IResourceWriteResult>());

        var locator = new ResourceLocator(new IResourceReader[0], new[] { w1.Object, w2.Object }, _tenantContextMock.Object);

        // Act
        await locator.WriteAsync(req, stream);

        // Assert
        w1.Verify(x => x.WriteAsync(It.IsAny<ResourceRequest>(), It.IsAny<Stream>(), true, null, null, default), Times.Never);
        w2.Verify(x => x.WriteAsync(It.IsAny<ResourceRequest>(), It.IsAny<Stream>(), true, null, null, default), Times.Once);
    }
}