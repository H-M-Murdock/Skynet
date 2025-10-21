using System.Collections.Concurrent;

namespace Skynet.Core.Logging;

/// <summary>
/// In-Memory-Sink mit festen Kapazitätsgrenzen (Ringpuffer).
/// - Bei Überlauf werden älteste Einträge entfernt (DropOldest).
/// - Thread-sicher.
/// - StartAsync/FlushAsync sind No-Op.
/// </summary>
public sealed class MemoryLogSink : ILogSink
{
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly Queue<ILogEvent> _queue;

    public MemoryLogSink(int capacity = 1000)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _queue = new Queue<ILogEvent>(Math.Min(capacity, 128));
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task WriteAsync(ILogEvent evt, CancellationToken ct)
    {
        if (evt is null) return Task.CompletedTask;

        lock (_lock)
        {
            if (_queue.Count >= _capacity)
                _queue.Dequeue(); // Drop oldest
            _queue.Enqueue(evt);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gibt eine Momentaufnahme der gespeicherten Events zurück (älteste → neueste).
    /// </summary>
    public IReadOnlyList<ILogEvent> Snapshot()
    {
        lock (_lock)
        {
            return _queue.ToArray();
        }
    }

    /// <summary>
    /// Leert den Puffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock) { _queue.Clear(); }
    }

    public int Count
    {
        get { lock (_lock) return _queue.Count; }
    }

    public int Capacity => _capacity;
}
