using System.Globalization;
using Skynet.Core.i18n;

namespace Skynet.Tests.i18n;

public class SimpleTemplateRendererTests
{
    private readonly SimpleTemplateRenderer _renderer = new();
    private readonly CultureInfo _de = new("de-DE");
    private readonly CultureInfo _us = new("en-US");

    [Fact]
    public void Render_ShouldReplaceSimpleKeys()
    {
        var tmpl = "Hello {Name}!";
        var p = new Dictionary<string, object?> { { "Name", "World" } };

        var result = _renderer.Render(tmpl, p, _us);

        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void Render_ShouldRespectCultureFormatting()
    {
        var tmpl = "Price: {Price:N2}";
        var p = new Dictionary<string, object?> { { "Price", 1234.56 } };

        var resultDe = _renderer.Render(tmpl, p, _de);
        var resultUs = _renderer.Render(tmpl, p, _us);

        Assert.Equal("Price: 1.234,56", resultDe); // DE Komma
        Assert.Equal("Price: 1,234.56", resultUs); // US Punkt
    }

    [Fact]
    public void Render_ShouldKeepMissingKeys()
    {
        var tmpl = "Hello {User}, welcome to {App}.";
        var p = new Dictionary<string, object?> { { "User", "Alice" } }; // App fehlt

        var result = _renderer.Render(tmpl, p, _us);

        Assert.Equal("Hello Alice, welcome to {App}.", result);
    }

    [Fact]
    public void Render_ShouldHandleEscapedBraces()
    {
        var tmpl = "Set {{Value}} to {Value}";
        var p = new Dictionary<string, object?> { { "Value", 42 } };

        var result = _renderer.Render(tmpl, p, _us);

        Assert.Equal("Set {Value} to 42", result);
    }

    [Fact]
    public void Render_ShouldHandleNullValuesAsEmptyString()
    {
        var tmpl = "Value is [{Val}]";
        var p = new Dictionary<string, object?> { { "Val", null } };

        var result = _renderer.Render(tmpl, p, _us);

        Assert.Equal("Value is []", result);
    }
    
    [Fact]
    public void Render_ShouldHandleWhitespaceInKeys()
    {
        // Toleranter Umgang mit Spaces im Key "{  Name  }"
        var tmpl = "Hello {  Name  }";
        var p = new Dictionary<string, object?> { { "Name", "Bob" } };

        var result = _renderer.Render(tmpl, p, _us);

        Assert.Equal("Hello Bob", result);
    }

    [Fact]
    public void Render_ShouldHandleColonWithoutFormat()
    {
        // Edge Case: Key heißt "My:Key" (ungewöhnlich, aber möglich wenn Dictionary es zulässt)
        // Unser Parser splittet am ersten ':', also "My" -> Key, "Key" -> Format.
        // Das ist das erwartete Verhalten für Standardformatierung.
        // Wenn der Key wirklich "My:Key" hieße, würde unser SimpleRenderer ihn nicht finden.
        // Testen wir das Standardverhalten:
        
        var tmpl = "{Value:D4}";
        var p = new Dictionary<string, object?> { { "Value", 5 } };
        
        var result = _renderer.Render(tmpl, p, _us);
        Assert.Equal("0005", result);
    }
}
