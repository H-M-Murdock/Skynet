using Moq;
using Skynet.Core.Localization;

namespace Skynet.Tests.Localization;

public class CurrentCultureProviderTests
{
    private readonly Mock<ILocalizationStore> _storeMock;
    private readonly LocalizationOptions _options;
    private readonly CurrentCultureProvider _provider;

    public CurrentCultureProviderTests()
    {
        _storeMock = new Mock<ILocalizationStore>();
        _options = new LocalizationOptions 
        { 
            DefaultCulture = "en-US",
            SupportedCultures = null // Allow all initially
        };
        _provider = new CurrentCultureProvider(_storeMock.Object, _options);
    }

    [Fact]
    public void GetCulture_ShouldReturnStoredCulture_WhenValid()
    {
        _storeMock.Setup(s => s.GetCultureName()).Returns("de-DE");
        var culture = _provider.GetCulture();
        Assert.Equal("de-DE", culture.Name);
    }

    [Fact]
    public void GetCulture_ShouldReturnDefault_WhenStoredIsNull()
    {
        _storeMock.Setup(s => s.GetCultureName()).Returns((string?)null);
        var culture = _provider.GetCulture();
        Assert.Equal("en-US", culture.Name);
    }

    [Fact]
    public void GetCulture_ShouldReturnDefault_WhenStoredIsNotSupported()
    {
        // Arrange: Whitelist set
        _options.SupportedCultures = new List<string> { "en-US", "fr-FR" };
        // Store returns something valid but NOT in whitelist
        _storeMock.Setup(s => s.GetCultureName()).Returns("de-DE");

        // Act
        var culture = _provider.GetCulture();

        // Assert
        Assert.Equal("en-US", culture.Name); // Fallback triggered
    }

    [Fact]
    public void GetCulture_ShouldReturnDefault_WhenStoredIsGibberish()
    {
        _storeMock.Setup(s => s.GetCultureName()).Returns("fantasy-lang");
        var culture = _provider.GetCulture();
        Assert.Equal("en-US", culture.Name);
    }
}
