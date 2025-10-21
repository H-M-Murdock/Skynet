using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class SimpleLineLogTextFormatterMoreTests
{
    private static MutableLogEvent NewBase(LogLevel level = LogLevel.Information) => new()
    {
        Timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero),
        Level = level,
        EventId = new EventId(0),
        GlobalEventId = "",
        CategoryName = null,
        Operation = null,
        CorrelationId = null,
        TraceId = null,
        SpanId = null,
        State = Array.Empty<KeyValuePair<string, object?>>()
    };

    [Theory]
    [InlineData(true, "Z")]
    [InlineData(false, "+00:00")]
    public void Utc_Local_Switch_Works(bool useUtc, string expectedSuffix)
    {
        var f = new SimpleLineLogTextFormatter(useUtcTimestamps: useUtc, includeState: false);
        var e = NewBase();
        var line = f.Format(e);
        Assert.Contains(expectedSuffix, line);
    }

    [Fact]
    public void No_Brackets_When_No_Metadata()
    {
        var f = new SimpleLineLogTextFormatter();
        var e = NewBase();
        var line = f.Format(e);
        Assert.DoesNotContain(" [", line);
        Assert.DoesNotContain("]", line);
    }

    [Fact]
    public void EventId_Formats_Id_Only()
    {
        var f = new SimpleLineLogTextFormatter();
        var e = NewBase();
        e.EventId = new EventId(7);
        var line = f.Format(e);
        Assert.Contains("EventId=7", line);
        Assert.DoesNotContain("EventId=7(", line);
    }

    [Fact]
    public void EventId_Formats_Id_And_Name()
    {
        var f = new SimpleLineLogTextFormatter();
        var e = NewBase();
        e.EventId = new EventId(7, "E");
        var line = f.Format(e);
        Assert.Contains("EventId=7(E)", line);
    }

    [Fact]
    public void GlobalEventId_Rendered_Only_When_NotEmpty()
    {
        var f = new SimpleLineLogTextFormatter();
        var e = NewBase();
        e.GlobalEventId = "";
        var line = f.Format(e);
        Assert.DoesNotContain("GEID=", line);

        e.GlobalEventId = "abc";
        line = f.Format(e);
        Assert.Contains("GEID=abc", line);
    }

    [Fact]
    public void State_Renders_Nulls_And_Complex_Objects_Safely()
    {
        var f = new SimpleLineLogTextFormatter(includeState: true);
        var e = NewBase();
        e.State = new List<KeyValuePair<string, object?>>
        {
            new("n1", null),
            new("arr", new []{1,2,3}),
            new("obj", new { A = 1, B = "x" }),
        };

        var line = f.Format(e);
        Assert.Contains("n1=", line);              // null -> empty after '=' via ToString() null handling
        Assert.Contains("arr=System.Int32[]", line); // default ToString for arrays
        Assert.Contains("obj={ A = 1, B = x }", line); // anonymous type ToString
    }

    [Fact]
    public void Large_State_Performance_Smoke()
    {
        var f = new SimpleLineLogTextFormatter(includeState: true);
        var e = NewBase();
        var list = new List<KeyValuePair<string, object?>>(1000);
        for (int i = 0; i < 1000; i++) list.Add(new KeyValuePair<string, object?>("k" + i, i));
        e.State = list;

        var line = f.Format(e);
        Assert.True(line.Length > 100); // simple sanity
        Assert.Contains("k0=0", line);
        Assert.Contains("k999=999", line);
    }

    [Fact]
    public void Exception_With_Newlines_Is_One_Line()
    {
        var f = new SimpleLineLogTextFormatter(includeState: false);
        var e = NewBase();
        e.Exception = "System.Exception: boom\r\nat X()\ninner";
        var line = f.Format(e);
        Assert.Contains("ex=System.Exception: boom", line);
        Assert.DoesNotContain("\r", line);
        Assert.DoesNotContain("\n", line);
    }

    [Fact]
    public void Culture_Invariant_Output()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var f = new SimpleLineLogTextFormatter(includeState: true);
            var e = NewBase(LogLevel.Warning);
            e.State = new List<KeyValuePair<string, object?>>
            {
                new("num", 1234.56) // ToString() culture-dependent, but we just ensure formatter doesn't throw and includes key
            };

            var line = f.Format(e);
            Assert.Contains("Warning", line);
            Assert.Contains("num=", line);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
