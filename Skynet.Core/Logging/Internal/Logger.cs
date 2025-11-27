using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

public sealed class Logger : ILogger
{
    private readonly string _categoryName;
    private readonly ILoggingClient _client;

    public Logger(string categoryName, ILoggingClient client)
    {
        _categoryName = categoryName;
        _client = client;
    }

    // Scopes sind komplexer, für Version 1.0 oft optional oder via AsyncLocal zu lösen.
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        // Event bauen
        // Hinweis: Hier wird es synchron gebaut, aber asynchron an den Client übergeben (Fire&Forget im Sinne von LogAsync ist ValueTask).
        // Für extrem hohe Performance könnte man Object-Pooling für MutableLogEvent nutzen.
        
        var evt = new MutableLogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = logLevel,
            EventId = eventId,
            CategoryName = _categoryName,
            ExceptionObj = exception,
            Exception = exception?.ToString(), // oder Formatierung
            // State extrahieren (vereinfacht)
            State = ExtractState(state)
        };

        // Fire & Forget, da ILogger.Log synchron ist. 
        // Der Client puffert intern, daher ist das await meist instantan.
        _ = _client.LogAsync(evt, CancellationToken.None);
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> ExtractState<TState>(TState state)
    {
        if (state is IReadOnlyList<KeyValuePair<string, object?>> list) return list;
        if (state is IEnumerable<KeyValuePair<string, object?>> enumerable) return enumerable.ToList();
        return Array.Empty<KeyValuePair<string, object?>>();
    }
}