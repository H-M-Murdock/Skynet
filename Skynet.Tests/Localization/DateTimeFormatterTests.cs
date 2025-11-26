using System.Globalization;
using Moq;
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization;

public class DateTimeFormatterTests
{
    private readonly Mock<ICurrentCultureProvider> _providerMock;
    private readonly DateTimeFormatter _formatter;

    public DateTimeFormatterTests()
    {
        _providerMock = new Mock<ICurrentCultureProvider>();
        _formatter = new DateTimeFormatter(_providerMock.Object);
    }

    [Fact]
    public void Format_ShouldUseCultureFromProvider()
    {
        // Arrange
        _providerMock.Setup(p => p.GetCulture()).Returns(new CultureInfo("de-DE"));
        var date = new DateTime(2023, 10, 31); // 31. Okt

        // Act
        // "d" in de-DE is usually dd.MM.yyyy
        var result = _formatter.Format(date, "ShortDate"); 

        // Assert
        Assert.Equal("31.10.2023", result);
    }

    [Fact]
    public void Format_ShouldUseUSFormat_WhenProviderReturnsUS()
    {
        // Arrange
        _providerMock.Setup(p => p.GetCulture()).Returns(new CultureInfo("en-US"));
        var date = new DateTime(2023, 10, 31); 

        // Act
        var result = _formatter.Format(date, "ShortDate");

        // Assert
        // "d" in en-US is usually M/d/yyyy
        Assert.Equal("10/31/2023", result);
    }

    [Fact]
    public void Format_ShouldSupportCustomPatterns()
    {
        // Arrange
        _providerMock.Setup(p => p.GetCulture()).Returns(CultureInfo.InvariantCulture);
        var date = new DateTime(2023, 1, 1);

        // Act
        var result = _formatter.Format(date, "yyyy-MM");

        // Assert
        Assert.Equal("2023-01", result);
    }
}
