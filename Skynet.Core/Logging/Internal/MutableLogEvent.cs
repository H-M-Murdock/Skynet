using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

public sealed class MutableLogEvent : ILogEvent
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