using System.Globalization;
using Moq;
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization;

public class CultureThreadScopeFactoryTests
{
    private readonly Mock<ICurrentCultureProvider> _providerMock;
    private readonly CultureThreadScopeFactory _factory;

    public CultureThreadScopeFactoryTests()
    {
        _providerMock = new Mock<ICurrentCultureProvider>();
        _factory = new CultureThreadScopeFactory(_providerMock.Object);
    }

    [Fact]
    public void BeginScope_ShouldApplyProviderCulture_AndRestoreOnDispose()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        var target = new CultureInfo("fr-FR");
        
        _providerMock.Setup(p => p.GetCulture()).Returns(target);

        // Act & Assert
        using (_factory.BeginScope())
        {
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
            Assert.Equal("fr-FR", CultureInfo.CurrentUICulture.Name);
        }

        // Restore check
        Assert.Equal(original.Name, CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public void BeginScope_Override_ShouldApplyOverride_AndRestoreOnDispose()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        var overrideCulture = new CultureInfo("es-ES");

        // Act & Assert
        using (_factory.BeginScope(overrideCulture))
        {
            Assert.Equal("es-ES", CultureInfo.CurrentCulture.Name);
        }

        Assert.Equal(original.Name, CultureInfo.CurrentCulture.Name);
    }
}
