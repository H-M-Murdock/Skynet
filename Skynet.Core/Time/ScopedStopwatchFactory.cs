using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;

namespace Skynet.Core.Time;

public class ScopedStopwatchFactory
{
    private readonly IServiceProvider _sp;
    private readonly IStopwatch _stopwatch;

    public ScopedStopwatchFactory(IStopwatch stopwatch, IServiceProvider sp)
    {
        _stopwatch = stopwatch ?? throw new ArgumentNullException(nameof(stopwatch));
        _sp = sp ?? throw new ArgumentNullException(nameof(sp));
    }

    public IDisposable Start(string operation, LogLevel level = LogLevel.Information, Dictionary<string, object?>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation darf nicht leer sein.", nameof(operation));

        return new ScopedStopwatch(_stopwatch, elapsed =>
        {
            var state = new List<KeyValuePair<string, object?>>(properties?.Count ?? 0 + 2);
            if (properties is not null)
            {
                foreach (var kv in properties)
                    state.Add(new KeyValuePair<string, object?>(kv.Key, kv.Value));
            }
            state.Add(new KeyValuePair<string, object?>("ElapsedMs", elapsed.TotalMilliseconds));
            state.Add(new KeyValuePair<string, object?>("Operation", operation));

            var evt = new MutableLogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                EventId = new EventId(0, "ScopedTiming"),
                GlobalEventId = Guid.NewGuid().ToString("n"),
                CategoryName = null,
                Operation = operation,
                State = state
            };

            try
            {
                var client = _sp.GetService<ILoggingClient>();
                if (client is not null)
                    _ = client.LogAsync(evt, CancellationToken.None);
            }
            catch { /* niemals werfen */ }
        });
    }
}
