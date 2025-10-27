namespace Skynet.Core.Logging;

public sealed class DefaultLogEventMaterializer : ILogEventMaterializer
{
    public ILogEvent Materialize(ILogEvent s)
    {
        // Deep copy: neue MutableLogEvent-Instanz + kopierte State-Liste
        return new MutableLogEvent
        {
            Timestamp     = s.Timestamp,
            Level         = s.Level,
            EventId       = s.EventId,
            GlobalEventId = s.GlobalEventId ?? string.Empty,
            CategoryName  = s.CategoryName,
            Operation     = s.Operation,
            State         = s.State is null
                ? Array.Empty<KeyValuePair<string, object?>>()
                : s.State is List<KeyValuePair<string, object?>> list
                    ? new List<KeyValuePair<string, object?>>(list)
                    : s.State.ToArray(),
            ExceptionObj  = s.ExceptionObj,
            Exception     = s.Exception,
            CorrelationId = s.CorrelationId,
            TraceId       = s.TraceId,
            SpanId        = s.SpanId
        };
    }
}