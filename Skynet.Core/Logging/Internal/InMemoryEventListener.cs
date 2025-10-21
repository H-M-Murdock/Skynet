using System.Collections.Concurrent;

namespace Skynet.Core.Logging;

/// <summary>
/// In-Memory-Listener für Tests:
/// - BindAsync markiert "gebunden".
/// - AcceptAsync liefert Kanäle aus einer internen Queue (Push via EnqueueChannel).
/// - CloseAsync markiert beendet; AcceptAsync wirft dann OperationCanceledException.
/// Thread-sicher.
/// </summary>
public sealed class InMemoryEventListener : IEventListener
{
    private readonly ConcurrentQueue<IEventChannel> _pending = new();
    private volatile bool _bound;
    private volatile bool _closed;

    public void EnqueueChannel(IEventChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _pending.Enqueue(channel);
    }

    public Task BindAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_closed)
            throw new InvalidOperationException("Listener is closed.");
        _bound = true;
        return Task.CompletedTask;
    }

    public Task<IEventChannel> AcceptAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_bound) throw new InvalidOperationException("Listener not bound.");
        if (_closed) throw new OperationCanceledException("Listener closed.");

        if (_pending.TryDequeue(out var ch))
            return Task.FromResult(ch);

        // Keine neuen Verbindungen verfügbar: blockierende Variante vermeiden (einfaches Verhalten für Tests)
        // Hier kurzer Yield und dann "keine Verbindung": wirft, damit Tests deterministisch sind.
        throw new InvalidOperationException("No pending channels.");
    }

    public Task CloseAsync(CancellationToken ct)
    {
        _closed = true;
        return Task.CompletedTask;
    }
}
