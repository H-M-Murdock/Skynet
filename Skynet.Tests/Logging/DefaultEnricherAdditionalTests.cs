using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class DefaultEnricherAdditionalTests
{
    [Fact]
    public void Does_Not_Override_Existing_Correlation_Trace_Span()
    {
        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr",
            TraceId = "trace",
            SpanId = "span"
        };

        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.Equal("corr", e.CorrelationId);
        Assert.Equal("trace", e.TraceId);
        Assert.Equal("span", e.SpanId);
    }

    [Fact]
    public void Without_Activity_Sets_New_Correlation_Only()
    {
        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = null,
            TraceId = null,
            SpanId = null
        };

        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.False(string.IsNullOrWhiteSpace(e.CorrelationId));
        Assert.Null(e.TraceId);
        Assert.Null(e.SpanId);
    }

    [Fact]
    public void EventId_And_Level_Remain_Unchanged()
    {
        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Warning,
            EventId = new EventId(123, "MyEvent"),
            GlobalEventId = "" // wird gesetzt
        };

        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.Equal(LogLevel.Warning, e.Level);
        Assert.Equal(new EventId(123, "MyEvent"), e.EventId);
        Assert.False(string.IsNullOrWhiteSpace(e.GlobalEventId));
    }

    [Fact]
    public void State_Remains_Unchanged()
    {
        var initial = new List<KeyValuePair<string, object?>>
        {
            new("A", 1),
            new("B", "x")
        };

        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            State = initial
        };

        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.Same(initial, e.State);
        Assert.Collection(e.State,
            kv => { Assert.Equal("A", kv.Key); Assert.Equal(1, kv.Value); },
            kv => { Assert.Equal("B", kv.Key); Assert.Equal("x", kv.Value); });
    }

    [Fact]
    public void Exception_Remains_Unchanged()
    {
        var ex = new InvalidOperationException("boom");
        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionObj = ex,
            Exception = "System.InvalidOperationException: boom"
        };

        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.Same(ex, e.ExceptionObj);
        Assert.Equal("System.InvalidOperationException: boom", e.Exception);
    }

    [Fact]
    public void GlobalEventId_Not_Overwritten_When_Already_Set()
    {
        var preset = "d" + Guid.NewGuid().ToString("n")[1..];
        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            GlobalEventId = preset
        };

        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.Equal(preset, e.GlobalEventId);
    }

    [Fact]
    public void With_Activity_Does_Not_Override_Existing_Ids()
    {
        using var act = new Activity("test").Start();
        var e = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "keep-corr",
            TraceId = "keep-trace",
            SpanId = "keep-span"
        };

        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.Equal("keep-corr", e.CorrelationId);
        Assert.Equal("keep-trace", e.TraceId);
        Assert.Equal("keep-span", e.SpanId);
    }
}
