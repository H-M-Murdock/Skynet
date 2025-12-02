using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

public sealed class BootstrapLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ILogSink _sink;

    public BootstrapLogger(string categoryName, ILogSink sink)
    {
        _categoryName = categoryName;
        _sink = sink;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null; // Scope support optional für Bootstrap

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        // 1. LogEvent bauen
        var evt = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = logLevel,
            EventId = eventId,
            CategoryName = _categoryName,
            ExceptionObj = exception,
            Exception = exception?.ToString(),
            GlobalEventId = Guid.NewGuid().ToString("N"), // Oder GuidV7 wenn verfügbar
            TraceId = System.Diagnostics.Activity.Current?.TraceId.ToString(),
            SpanId = System.Diagnostics.Activity.Current?.SpanId.ToString()
        };

        // 2. State extrahieren (für strukturierte Logs)
        if (state is IReadOnlyList<KeyValuePair<string, object?>> kvList)
        {
            evt.State = kvList;
        }
        else if (state is IEnumerable<KeyValuePair<string, object?>> kvEnum)
        {
            evt.State = kvEnum.ToList();
        }
        else
        {
            // Fallback für unstrukturierte Strings
            evt.State = new[] { new KeyValuePair<string, object?>("Message", state?.ToString()) };
        }

        // 3. In die Sink schreiben (Fire & Forget im synchronen Log-Kontext, aber sicher gepuffert)
        // Da Log synchron ist, müssen wir Task.Run oder ähnliches vermeiden, um Blocking zu verhindern.
        // Im Bootstrap ist es okay, kurz zu warten oder _sink.WriteAsync zu feuern.
        // Sauberer Weg: .Wait() oder .Result ist gefährlich. 
        // Da es Bootstrap ist (Single Threaded), riskieren wir hier den asynchronen Aufruf ohne await.
        _ = _sink.WriteAsync(evt, CancellationToken.None);
    }
}