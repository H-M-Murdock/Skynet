using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace Skynet.Core.Logging;

/// <summary>
/// In-Memory Transport für Tests/Dev:
/// - Erstellt beim Connect einen internen "WritableChannel", der als IEventChannel in den InMemoryEventListener eingespeist wird.
/// - TrySendAsync ist non-blocking und respektiert Kapazitätsgrenzen (Backpressure).
/// - FlushAsync wartet, bis der Transportpuffer leer ist (d.h. alles an den Channel übergeben wurde und vom Server abgebaut ist).
/// - CloseAsync beendet den Kanal (Server erhält End-of-Stream).
/// Thread-sicher; für Produktiv-Nutzung als Referenz/Adapter gedacht.
/// </summary>
public sealed class InMemoryEventTransport : IEventTransport
{
    private readonly InMemoryEventListener _listener;
    private readonly int _capacity;

    private volatile TransportState _state = TransportState.Disconnected;
    public TransportState State => _state;

    private WritableChannel? _channel;

    /// <param name="listener">Zugehöriger InMemoryEventListener des Servers (gleicher Prozess/Testlauf).</param>
    /// <param name="capacity">Maximale Anzahl gepufferter Frames im Transport (Backpressure-Grenze).</param>
    public InMemoryEventTransport(InMemoryEventListener listener, int capacity = 1024)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _capacity = Math.Max(1, capacity);
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_state is TransportState.Connected or TransportState.Connecting)
            return;

        _state = TransportState.Connecting;

        // Sicherstellen, dass der Listener gebunden ist (Bind ist idempotent beim InMemoryListener)
        await _listener.BindAsync(ct).ConfigureAwait(false);

        // Channel erstellen und dem Listener anbieten (Server-Accept wird ihn abholen).
        var ch = new WritableChannel(_capacity);
        _channel = ch;
        _listener.EnqueueChannel(ch);

        _state = TransportState.Connected;
    }

    public ValueTask<bool> TrySendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_state != TransportState.Connected || _channel is null)
            return ValueTask.FromResult(false);

        try
        {
            return ValueTask.FromResult(_channel.TryWrite(payload, ct));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(false);
        }
        catch (Exception)
        {
            _state = TransportState.Faulted;
            return ValueTask.FromResult(false);
        }
    }

    public Task FlushAsync(CancellationToken ct)
    {
        // Für InMemory: wir warten, bis der WritableChannel leer ist (keine ausstehenden Items).
        var ch = _channel;
        if (_state != TransportState.Connected || ch is null)
            return Task.CompletedTask;

        return ch.WaitUntilDrainedAsync(ct);
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        var ch = _channel;
        if (ch is null)
        {
            _state = TransportState.Closed;
            return;
        }

        try
        {
            // Signalisiere End-of-Stream für den Server (ReadAsync -> null nach Entleerung).
            await ch.CloseAsync(ct).ConfigureAwait(false);
            _state = TransportState.Closed;
        }
        catch
        {
            _state = TransportState.Faulted;
            throw;
        }
    }

    /// <summary>
    /// Schreibbarer InMemory-Channel:
    /// - Produzenten (Transport) rufen TryWrite() non-blocking auf.
    /// - Konsument (Server) ruft ReadAsync(ct) auf; blockiert, bis Item oder End-of-Stream.
    /// </summary>
    private sealed class WritableChannel : IEventChannel
    {
        private readonly BlockingCollection<ReadOnlyMemory<byte>> _buffer;
        private int _inflight;

        public WritableChannel(int capacity)
        {
            // Bounded: kontrolliertes Backpressure
            _buffer = new BlockingCollection<ReadOnlyMemory<byte>>(new ConcurrentQueue<ReadOnlyMemory<byte>>(), capacity);
            _inflight = 0;
        }

        public bool TryWrite(ReadOnlyMemory<byte> frame, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            // non-blocking: TryAdd mit 0-Timeout
            var ok = _buffer.TryAdd(frame, 0, ct);
            if (ok) Interlocked.Increment(ref _inflight);
            return ok;
        }

        public async Task<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken ct)
        {
            // Blockiert, bis ein Item da ist oder der Kanal geschlossen wurde
            try
            {
                var item = _buffer.Take(ct);
                Interlocked.Decrement(ref _inflight);
                return await Task.FromResult<ReadOnlyMemory<byte>?>(item).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // CompleteAdding() wurde aufgerufen und Buffer ist leer -> End-of-Stream
                return null;
            }
        }

        public Task CloseAsync(CancellationToken ct)
        {
            // Stoppt nur den Producer. Der Consumer (Server) kann den Puffer
            // danach immer noch vollständig leeren. Die destruktive `while`-Schleife
            // wurde entfernt, da sie die Ursache des Fehlers war.
            _buffer.CompleteAdding();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _buffer.Dispose();
            return ValueTask.CompletedTask;
        }

        public Task WaitUntilDrainedAsync(CancellationToken ct)
        {
            // Warten, bis keine in-flight Items mehr existieren UND der Queue-Count 0 ist.
            // (Count ist nur eine Momentaufnahme; Kombination mit inflight ist robust genug für Tests/Dev)
            return Task.Run(() =>
            {
                SpinWait sw = new();
                while (!ct.IsCancellationRequested)
                {
                    if (_buffer.Count == 0 && Volatile.Read(ref _inflight) == 0)
                        break;
                    sw.SpinOnce();
                }

                ct.ThrowIfCancellationRequested();
            }, ct);
        }
    }
}
