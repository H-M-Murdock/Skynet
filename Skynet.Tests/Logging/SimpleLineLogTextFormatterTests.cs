using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class SimpleLineLogTextFormatterTests
{
    public static SimpleLineLogTextFormatterTests CreateInstance()
    {
        return new SimpleLineLogTextFormatterTests();
    }

    private static MutableLogEvent NewEvt() => new()
    {
        Timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero),
        Level = LogLevel.Warning,
        EventId = new EventId(42, "TestEvent"),
        GlobalEventId = "geid",
        CategoryName = "Cat",
        Operation = "Op",
        CorrelationId = "corr",
        TraceId = "tr",
        SpanId = "sp",
        State = new List<KeyValuePair<string, object?>>
        {
            new("a", 1),
            new("b", "x")
        }
    };

    [Fact]
    public void Formats_Basic_Line()
    {
        var f = new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: true);
        var e = NewEvt();

        var line = f.Format(e);
        Assert.Contains("2024-12-31T23:59:58.0000000Z", line);
        Assert.Contains("Warning", line);
        Assert.Contains("Cat", line);
        Assert.Contains("EventId=42(TestEvent)", line);
        Assert.Contains("GEID=geid", line);
        Assert.Contains("Op=Op", line);
        Assert.Contains("Corr=corr", line);
        Assert.Contains("Trace=tr", line);
        Assert.Contains("Span=sp", line);
        Assert.Contains("a=1", line);
        Assert.Contains("b=x", line);
    }

    [Fact]
    public void Skips_Nulls_And_Empty_State()
    {
        var f = new SimpleLineLogTextFormatter(includeState: true);
        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(0),
            Level = LogLevel.Information,
            EventId = new EventId(0),
            GlobalEventId = "",
            CategoryName = null,
            State = Array.Empty<KeyValuePair<string, object?>>()
        };

        var line = f.Format(e);
        Assert.DoesNotContain("Cat=", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[]", line);
    }

    [Fact]
    public void Without_State_Does_Not_Render_State()
    {
        var f = new SimpleLineLogTextFormatter(includeState: false);
        var e = NewEvt();

        var line = f.Format(e);
        Assert.DoesNotContain(" a=1", line);
    }

    [Fact]
    public void Includes_Exception_If_Present()
    {
        var f = new SimpleLineLogTextFormatter();
        var e = NewEvt();
        e.Exception = "System.Exception: boom";

        var line = f.Format(e);
        Assert.Contains("ex=System.Exception: boom", line);
    }
}
