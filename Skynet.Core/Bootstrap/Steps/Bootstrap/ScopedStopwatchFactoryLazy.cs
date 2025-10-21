using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Logging;
using Skynet.Core.Time;

namespace Skynet.Core.Bootstrap;

sealed class LazyScopedStopwatchFactory : ScopedStopwatchFactory
{
    private readonly IServiceProvider _sp;
    private readonly IStopwatch _stopwatch;

    public LazyScopedStopwatchFactory(IServiceProvider sp, IStopwatch stopwatch)
        : base(stopwatch, new NoopLoggingClient()) // Basis benötigt einen Client, wir geben No-Op
    {
        _sp = sp ?? throw new ArgumentNullException(nameof(sp));
        _stopwatch = stopwatch ?? throw new ArgumentNullException(nameof(stopwatch));
    }

    // Schatten-API: Wir “verstecken” die Basismethode mit new und implementieren Lazy-Logging.
    public new IDisposable Start(string operation, string level = "Information", Dictionary<string, object?>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation darf nicht leer sein.", nameof(operation));

        return new ScopedStopwatch(_stopwatch, elapsed =>
        {
            var props = properties is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(properties, StringComparer.Ordinal);

            props["ElapsedMs"] = elapsed.TotalMilliseconds;

            var evt = new ScopedTimingEvent(
                timestamp: DateTimeOffset.UtcNow,
                level: level,
                messageTemplate: "Timing {Operation} took {ElapsedMs} ms",
                props: props,
                operation: operation
            );

            try
            {
                var client = _sp.GetService<ILoggingClient>();
                if (client is not null)
                {
                    _ = SafeFireAndForgetLogAsync(client, evt);
                }
                // kein Client verfügbar -> no-op
            }
            catch
            {
                // niemals werfen
            }
        });
    }

    private static async Task SafeFireAndForgetLogAsync(ILoggingClient client, ILogEvent evt)
    {
        try
        {
            await client.LogAsync(evt, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best Effort
        }
    }

    // Lokales minimales Event (analog zum in ScopedStopwatchFactory verwendeten)
    private sealed class ScopedTimingEvent : ILogEvent
    {
        public ScopedTimingEvent(DateTimeOffset timestamp, string level, string messageTemplate, IReadOnlyDictionary<string, object?> props, string operation)
        {
            Timestamp = timestamp;
            Level = level;
            MessageTemplate = messageTemplate;
            Properties = props;
            Operation = operation;
            EventId = Guid.NewGuid().ToString("n");
        }

        public DateTimeOffset Timestamp { get; }
        public string Level { get; }
        public string MessageTemplate { get; }
        public IReadOnlyDictionary<string, object?> Properties { get; }
        public string? Exception { get; } = null;
        public string? SourceContext { get; } = null;
        public string? Operation { get; }
        public int? EventCode { get; } = null;
        public string EventId { get; }
        public string? CorrelationId { get; } = null;
    }

    private sealed class NoopLoggingClient : ILoggingClient
    {
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask LogAsync(ILogEvent evt, CancellationToken ct) => ValueTask.CompletedTask;
        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(bool drain, CancellationToken ct) => Task.CompletedTask;
        public long DroppedCount => 0;
        public int QueueLength => 0;
        public int QueueCapacity => 0;
    }
}