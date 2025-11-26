using Skynet.Core.i18n;

namespace Skynet.Tests.i18n;

public class I18NTemplateTests
{
    [Fact]
    public void Initialization_ShouldSetProperties()
    {
        // Arrange
        var key = new I18NKey("ui", "label.login");
        var text = "Anmelden";
        var now = DateTimeOffset.UtcNow;

        // Act
        var tmpl = new I18NTemplate
        {
            Key = key,
            Template = text,
            Source = "seed",
            LastModified = now
        };

        // Assert
        Assert.Equal(key, tmpl.Key);
        Assert.Equal(text, tmpl.Template);
        Assert.Equal("seed", tmpl.Source);
        Assert.Equal(now, tmpl.LastModified);
        Assert.Null(tmpl.ContentType); // Default Check
    }

    [Fact]
    public void Equality_ShouldBeValueBased()
    {
        // Arrange
        var key = new I18NKey("a", "b");
        var t1 = new I18NTemplate { Key = key, Template = "Foo", Version = "1" };
        var t2 = new I18NTemplate { Key = key, Template = "Foo", Version = "1" };
        var t3 = new I18NTemplate { Key = key, Template = "Bar", Version = "2" };

        // Assert
        Assert.Equal(t1, t2);
        Assert.NotEqual(t1, t3);
        Assert.True(t1 == t2);
    }

    [Fact]
    public void WithUpdate_ShouldCreateNewInstanceWithChanges()
    {
        // Arrange
        var original = new I18NTemplate 
        { 
            Key = new I18NKey("dom", "name"), 
            Template = "Old", 
            Version = "v1" 
        };
        var newTime = DateTimeOffset.UtcNow;

        // Act
        var updated = original.WithUpdate("New", "v2", newTime);

        // Assert
        // Original bleibt unverändert
        Assert.Equal("Old", original.Template);
        
        // Kopie ist aktualisiert
        Assert.Equal("New", updated.Template);
        Assert.Equal("v2", updated.Version);
        Assert.Equal(newTime, updated.LastModified);
        
        // Unveränderte Properties werden übernommen
        Assert.Equal(original.Key, updated.Key);
    }

    // Hinweis: Tests für 'required' Modifier sind Unit-Tests schwer direkt abzubilden,
    // da der Compiler den Code gar nicht erst kompilieren würde, wenn Felder fehlen.
}
