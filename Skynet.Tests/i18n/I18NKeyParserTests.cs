using Skynet.Core.i18n;

namespace Skynet.Tests.i18n;

public class I18NKeyTests
{
    [Fact]
    public void ToString_ShouldReturnCorrectFormat()
    {
        // Arrange
        var key = new I18NKey("errors", "Validation.MissingField");

        // Act
        var result = key.ToString();

        // Assert
        Assert.Equal("errors:Validation.MissingField", result);
    }

    [Fact]
    public void ToString_ShouldReturnEmpty_WhenDefault()
    {
        // Arrange
        var key = default(I18NKey);

        // Act
        var result = key.ToString();

        // Assert
        Assert.Equal(string.Empty, result);
    }
    
    [Fact]
    public void Empty_ShouldReturnDefault()
    {
        // Act
        var empty = I18NKey.Empty;
        
        // Assert
        Assert.Equal(default(I18NKey), empty);
        Assert.Equal(string.Empty, empty.ToString());
    }

    [Theory]
    [InlineData("errors:Validation.Missing", "errors", "Validation.Missing")]
    [InlineData("ui:Button:Save", "ui", "Button:Save")] // Name darf Doppelpunkte enthalten
    [InlineData("  logs  :  System.Start  ", "logs", "System.Start")] // Whitespace trimmen
    public void Parse_ShouldCreateCorrectKey(string input, string expectedDomain, string expectedName)
    {
        // Act
        var key = I18NKey.Parse(input);

        // Assert
        Assert.Equal(expectedDomain, key.Domain);
        Assert.Equal(expectedName, key.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("JustADomain")]
    [InlineData(":MissingDomain")]
    [InlineData("MissingName:")]
    public void Parse_ShouldThrow_OnInvalidFormat(string? input)
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => I18NKey.Parse(input!));
    }

    [Fact]
    public void TryParse_ShouldReturnTrue_OnValidInput()
    {
        // Act
        var success = I18NKey.TryParse("domain:name", out var key);

        // Assert
        Assert.True(success);
        Assert.Equal("domain", key.Domain);
        Assert.Equal("name", key.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void TryParse_ShouldReturnFalse_OnInvalidInput(string? input)
    {
        // Act
        var success = I18NKey.TryParse(input, out var key);

        // Assert
        Assert.False(success);
        Assert.Equal(default, key);
    }

    [Fact]
    public void Equality_ShouldWorkValueBased()
    {
        // Arrange
        var key1 = new I18NKey("a", "b");
        var key2 = I18NKey.Parse("a:b");
        var key3 = new I18NKey("a", "c");

        // Assert
        Assert.Equal(key1, key2);
        Assert.True(key1 == key2);
        Assert.NotEqual(key1, key3);
        Assert.True(key1 != key3);
    }
}