using System;
using Skynet.Core.Logging;

namespace Skynet.Core.Time
{
    /// <summary>
    /// Erzeugt ScopedStopwatch-Instanzen, die beim Dispose ein LogEvent über ILoggingClient senden.
    /// </summary>
    public class ScopedStopwatchFactory
    {
        private readonly IStopwatch _stopwatch;
        private readonly ILoggingClient _loggingClient;

        public ScopedStopwatchFactory(IStopwatch stopwatch, ILoggingClient loggingClient)
        {
            _stopwatch = stopwatch ?? throw new ArgumentNullException(nameof(stopwatch));
            _loggingClient = loggingClient ?? throw new ArgumentNullException(nameof(loggingClient));
        }

        /// <summary>
        /// Startet eine Messung und gibt ein IDisposable zurück, das beim Dispose ein LogEvent schreibt.
        /// </summary>
        /// <param name="operation">Operationsname (z. B. "OrderService.Process").</param>
        /// <param name="level">Log-Level (Default: Information).</param>
        /// <param name="properties">Optionale zusätzliche Properties.</param>
        public IDisposable Start(string operation, string level = "Information", Dictionary<string, object?>? properties = null)
        {
            if (string.IsNullOrWhiteSpace(operation))
                throw new ArgumentException("Operation darf nicht leer sein.", nameof(operation));

            properties ??= new Dictionary<string, object?>(StringComparer.Ordinal);

            return new ScopedStopwatch(_stopwatch, elapsed =>
            {
                var elapsedMs = elapsed.TotalMilliseconds;

                var props = new Dictionary<string, object?>(properties, StringComparer.Ordinal)
                {
                    ["ElapsedMs"] = elapsedMs
                };

                var evt = new ScopedTimingEvent(
                    timestamp: DateTimeOffset.UtcNow,
                    level: level,
                    messageTemplate: "Timing {Operation} took {ElapsedMs} ms",
                    props: props,
                    operation: operation
                );

                // Strikt asynchron entkoppeln UND Fehler abfangen
                _ = SafeFireAndForgetLogAsync(evt);
            });
        }

        private async Task SafeFireAndForgetLogAsync(ILogEvent evt)
        {
            try
            {
                await _loggingClient.LogAsync(evt, default).ConfigureAwait(false);
            }
            catch
            {
                // Best Effort: niemals Exceptions nach außen werfen
            }
        }

        private sealed class ScopedTimingEvent : ILogEvent
        {
            public ScopedTimingEvent(
                DateTimeOffset timestamp,
                string level,
                string messageTemplate,
                IReadOnlyDictionary<string, object?> props,
                string operation)
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
    }
}
