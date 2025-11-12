using System.Threading.Channels;
using System.Threading;

namespace Skynet.Core.Logging;

/// <summary>
/// - BindAsync markiert "gebunden".
/// - AcceptAsync liefert Kanäle aus einer internen Queue (Push via EnqueueChannel).
/// - CloseAsync markiert beendet; AcceptAsync wirft dann OperationCanceledException.
/// Thread-sicher.
/// </summary>
public sealed class InMemoryEventListener : IEventListener
{
    private readonly Channel<IEventChannel> _pending = Channel.CreateUnbounded<IEventChannel>();
    private volatile bool _bound;
    private int _closed; // 0 = offen, 1 = geschlossen

    public void EnqueueChannel(IEventChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _pending.Writer.TryWrite(channel);
    }

    public Task BindAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_closed == 1)
            throw new InvalidOperationException("Listener is closed.");
        _bound = true;
        return Task.CompletedTask;
    }

    public async Task<IEventChannel> AcceptAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_bound) throw new InvalidOperationException("Listener not bound.");
        if (_closed == 1) throw new OperationCanceledException("Listener closed.");

        try
        {
            // Asynchron auf einen Channel warten, statt eine Ausnahme zu werfen
            return await _pending.Reader.ReadAsync(ct);
        }
        catch (ChannelClosedException)
        {
            // Dieses Verhalten tritt auf, wenn CloseAsync aufgerufen wurde.
            // Wir simulieren das ursprüngliche Verhalten für Konsistenz.
            throw new OperationCanceledException("Listener closed.");
        }
    }

    public Task CloseAsync(CancellationToken ct)
    {
        // Mit Interlocked sicherstellen, dass der Code nur einmal ausgeführt wird.
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return Task.CompletedTask; // Bereits geschlossen, also nichts tun.
        }

        // Den Channel als "abgeschlossen" markieren.
        _pending.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
