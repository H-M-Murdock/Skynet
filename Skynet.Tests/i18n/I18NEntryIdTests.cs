using Skynet.Core.i18n;

namespace Skynet.Tests.i18n;

public class I18NEntryIdTests
{
    [Fact]
    public void Constructor_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var key = new I18NKey("ui", "btn.save");
        var scope = "tenant/1";
        var culture = "de-DE";

        // Act
        var entryId = new I18NEntryId(guid, key, scope, culture);

        // Assert
        Assert.Equal(guid, entryId.Id);
        Assert.Equal(key, entryId.Key);
        Assert.Equal(scope, entryId.TenantScope);
        Assert.Equal(culture, entryId.Culture);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTenantScopeIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new I18NEntryId(Guid.NewGuid(), new I18NKey("a", "b"), null!, "de"));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCultureIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new I18NEntryId(Guid.NewGuid(), new I18NKey("a", "b"), "scope", null!));
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var guid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var key = new I18NKey("domain", "name");
        var entryId = new I18NEntryId(guid, key, "system", "en-US");

        // Act
        var str = entryId.ToString();

        // Assert
        Assert.Equal("domain:name @ system [en-US] (00000000-0000-0000-0000-000000000001)", str);
    }

    [Fact]
    public void CreateNew_ShouldGenerateNewGuid()
    {
        // Act
        var entry = I18NEntryId.CreateNew(new I18NKey("d", "n"), "s", "c");

        // Assert
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal("d", entry.Key.Domain);
    }
}
