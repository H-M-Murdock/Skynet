using System.Text;
using Moq;
using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;

namespace Skynet.Tests.tenant;

public class SystemTenantResourcesTests
{
    private readonly Mock<IResourceMaterializer> _materializerMock;
    private readonly Mock<IResourceLocator> _locatorMock;
    private readonly SystemTenantResources _sut;

    public SystemTenantResourcesTests()
    {
        _materializerMock = new Mock<IResourceMaterializer>();
        _locatorMock = new Mock<IResourceLocator>();
        
        _sut = new SystemTenantResources(_materializerMock.Object, _locatorMock.Object);
    }

    [Fact]
    public async Task ConfigValueAsync_Should_Request_ConfigDir_With_JsonExtension()
    {
        // Arrange
        var key = "app";
        var expectedLogicalKey = "config/app.json";
        
        // Mock setup: TryGetAsync returns (true, jsonString)
        _materializerMock
            .Setup(m => m.TryGetAsync<string>(
                It.IsAny<ITenantContext>(), 
                expectedLogicalKey, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "{\"foo\":\"bar\"}"));

        // Act
        var result = await _sut.ConfigValueAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("{\"foo\":\"bar\"}", result);

        // Verify call
        _materializerMock.Verify(m => m.TryGetAsync<string>(
            It.Is<ITenantContext>(c => c.CurrentTenantId.Value == SystemTenantResources.SystemTenantGuid), 
            expectedLogicalKey, 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task AssetAsync_Should_Use_Locator_And_Return_AssetData()
    {
        // Arrange
        var path = "branding/logo.png";
        var expectedKey = "assets/branding/logo.png";
        var contentBytes = Encoding.UTF8.GetBytes("fake-image");
        using var stream = new MemoryStream(contentBytes);

        // Mock für das IResourceResult
        var resourceResultMock = new Mock<IResourceResult>();
        resourceResultMock.SetupGet(r => r.Content).Returns(stream);
        resourceResultMock.SetupGet(r => r.ContentType).Returns("image/png");

        // Setup Locator
        _locatorMock
            .Setup(l => l.TryGetAsync(It.IsAny<ResourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceLookupResult.Found(resourceResultMock.Object));

        // Act
        var result = await _sut.AssetAsync(path);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("logo.png", result.FileName);
        Assert.Equal("image/png", result.ContentType);
        
        // Verify Locator Request
        _locatorMock.Verify(l => l.TryGetAsync(
            It.Is<ResourceRequest>(r => 
                r.Key == expectedKey && 
                r.ResourceType == ResourceKind.Asset &&
                r.TenantId.Value == SystemTenantResources.SystemTenantGuid), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
    
    [Fact]
    public async Task CertificateAsync_Should_Iterate_Extensions_Until_Found()
    {
        // Arrange
        var name = "web";
        
        // 1. Simuliert: .pem nicht gefunden
        _locatorMock
            .Setup(l => l.TryGetAsync(
                It.Is<ResourceRequest>(r => r.Key.EndsWith(".pem")), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceLookupResult.NotFound());

        // 2. Simuliert: .crt auch nicht gefunden (wir testen nur die Iteration, ohne echten Cert-Parse)
        _locatorMock
            .Setup(l => l.TryGetAsync(
                It.Is<ResourceRequest>(r => r.Key.EndsWith(".crt")), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceLookupResult.NotFound());
        
        // Act
        await _sut.CertificateAsync(name);

        // Assert
        // Verify .pem Call
        _locatorMock.Verify(l => l.TryGetAsync(
            It.Is<ResourceRequest>(r => r.Key == "certs/web.pem"), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
            
        // Verify .crt Call
        _locatorMock.Verify(l => l.TryGetAsync(
            It.Is<ResourceRequest>(r => r.Key == "certs/web.crt"), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}