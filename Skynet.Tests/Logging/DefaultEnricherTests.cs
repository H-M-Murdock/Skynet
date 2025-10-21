using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

file sealed class MutableLogEvent : ILogEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; } = LogLevel.Information;
    public string LevelString => Level.ToInvariantString();
    public EventId EventId { get; set; } = new(0, null);
    public string GlobalEventId { get; set; } = "";
    public string? CategoryName { get; set; }
    public string? Operation { get; set; }
    public IReadOnlyList<KeyValuePair<string, object?>> State { get; set; } = Array.Empty<KeyValuePair<string, object?>>();
    public Exception? ExceptionObj { get; set; }
    public string? Exception { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
}

public sealed class DefaultEnricherTests
{
    [Fact]
    public void Generates_GlobalEventId_When_Missing()
    {
        var e = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow, GlobalEventId = "" };
        var enricher = new DefaultEnricher();
        enricher.Enrich(e);
        Assert.False(string.IsNullOrWhiteSpace(e.GlobalEventId));
    }

    [Fact]
    public void Uses_Activity_For_Correlation_And_Trace()
    {
        using var act = new Activity("test").Start();
        var e = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow };
        var enricher = new DefaultEnricher();
        enricher.Enrich(e);

        Assert.Equal(act.TraceId.ToString(), e.CorrelationId);
        Assert.Equal(act.TraceId.ToString(), e.TraceId);
        Assert.Equal(act.SpanId.ToString(), e.SpanId);
    }

    [Fact]
    public void Keeps_Existing_Correlation()
    {
        var e = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow, CorrelationId = "given" };
        var enricher = new DefaultEnricher();
        enricher.Enrich(e);
        Assert.Equal("given", e.CorrelationId);
    }
}
