using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class PathTemplateRendererTests
{
    private static MutableLogEvent NewEvt() => new()
    {
        Timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero),
        Level = LogLevel.Warning,
        EventId = new EventId(42, "E"),
        GlobalEventId = "geid",
        CategoryName = "Cat.Sub",
        Operation = "Op",
        CorrelationId = "corr",
        TraceId = "tr",
        SpanId = "sp",
        State = new List<KeyValuePair<string, object?>>
        {
            new("TenantId", "acme"),
            new("Component", "payments"),
        }
    };

    [Fact]
    public void Renders_Basic_Template_With_Known_Placeholders()
    {
        var evt = NewEvt();
        var r = new DefaultPathTemplateRenderer();

        var path = r.Render(evt, evt.Timestamp); // now = same as evt.Timestamp

        // Default template prüft nicht; wir testen eine konkrete Vorlage:
        var tmpl = "logs/{tenant}/{component}/{yyyy-MM-dd}/{level}.log";
        path = r.Render(evt, evt.Timestamp, tmpl);

        Assert.Equal("logs/acme/payments/2024-12-31/Warning.log", path);
    }

    [Fact]
    public void Sanitizes_Invalid_Characters_And_Skips_Empty_Segments()
    {
        var evt = NewEvt();
        evt.State = new List<KeyValuePair<string, object?>>
        {
            new("TenantId", "ac:me*"),
            new("Component", "pay?ments")
        };

        var r = new DefaultPathTemplateRenderer();
        var path = r.Render(evt, evt.Timestamp, "logs/{tenant}//{component}/{category}/{operation}.log");

        // ":" "*" "?" entfernt, doppelte Slashes reduziert, CategoryName "Cat.Sub" -> "Cat.Sub" erlaubt ('.' bleibt).
        Assert.Equal("logs/acme/payments/Cat.Sub/Op.log", path);
    }

    [Fact]
    public void Supports_DateTime_Tokens_And_Ids()
    {
        var evt = NewEvt();
        var r = new DefaultPathTemplateRenderer();

        var path = r.Render(evt, evt.Timestamp, "{yyyy}/{MM}/{dd}/{utc}/{eventId}/{globalId}.txt");
        Assert.Equal("2024/12/31/2024-12-31T23-59-58Z/42/geid.txt", path);
    }

    [Fact]
    public void Case_Insensitive_Tokens()
    {
        var evt = NewEvt();
        var r = new DefaultPathTemplateRenderer();

        var path = r.Render(evt, evt.Timestamp, "L/{TENANT}/{Component}/{LeVeL}.log");
        Assert.Equal("L/acme/payments/Warning.log", path);
    }

    [Fact]
    public void Empty_Unknown_Tokens_Are_Stripped()
    {
        var evt = NewEvt();
        var r = new DefaultPathTemplateRenderer();

        var path = r.Render(evt, evt.Timestamp, "x/{unknown}/{tenant}/y");
        Assert.Equal("x/acme/y", path);
    }

    [Fact]
    public void Falls_Back_When_State_Missing()
    {
        var evt = NewEvt();
        evt.State = Array.Empty<KeyValuePair<string, object?>>();
        var r = new DefaultPathTemplateRenderer();

        var path = r.Render(evt, evt.Timestamp, "{tenant}/{component}/{category}");
        // tenant/component fehlen -> überspringen; category vorhanden
        Assert.Equal("Cat.Sub", path);
    }
}
