namespace Skynet.Core.Logging;

using System.Threading.Channels;

/// <summary>
/// Verantwortlich für:
/// - Pufferung und asynchrones Senden von LogEvents.
/// - Nutzung von IBackpressurePolicy zur Entscheidung über Drop/Block-Verhalten.
/// - Batch-Verarbeitung und periodisches Flush.
/// - Integration in lokale InMemory-Transports.
/// </summary>
public sealed class LoggingClient : ILoggingClient, IAsyncDisposable
{
    private readonly IEventTransportFactory _transportFactory;
    private readonly ILogEventEncoder _encoder;
    private readonly IBackpressurePolicy _backpressurePolicy;
    private readonly LoggingClientOptions _options;

    // Enricher und Redaction Policies könnten hier optional injiziert werden (fehlen im aktuellen Konstruktor noch)
    // private readonly IEnumerable<IEnricher> _enrichers;
    // private readonly IRedactionPolicy _redactionPolicy;
    // private readonly ISizeLimiter _sizeLimiter;

    private readonly Channel<ILogEvent> _queue;
    private readonly CancellationTokenSource _cts = new();

    private IEventTransport? _transport;
    private Task? _senderTask;

    private long _droppedQueue;
    private long _droppedTransport;
    
    // NEU: Metriken als Properties implementieren
    public long DroppedCount => Interlocked.Read(ref _droppedQueue) + Interlocked.Read(ref _droppedTransport);
    public int QueueLength => _queue.Reader.Count;
    public int QueueCapacity => _options.QueueCapacity;

    public LoggingClient(
        IEventTransportFactory transportFactory,
        ILogEventEncoder encoder,
        IBackpressurePolicy backpressurePolicy,
        LoggingClientOptions? options = null)
    {
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _backpressurePolicy = backpressurePolicy ?? new SimpleBackpressurePolicy();
        _options = options ?? new LoggingClientOptions();

        var boundedOptions = new BoundedChannelOptions(Math.Max(1, _options.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _queue = Channel.CreateBounded<ILogEvent>(boundedOptions);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_transport != null)
            return; // Idempotent

        var transport = _transportFactory.Create();
        await transport.ConnectAsync(ct).ConfigureAwait(false);

        _transport = transport;
        _senderTask = Task.Run(() => SenderLoopAsync(_cts.Token), CancellationToken.None);
    }

    // Refaktorisiertes LogAsync (siehe vorherigen Schritt)
    public async ValueTask LogAsync(ILogEvent evt, CancellationToken ct)
    {
        if (evt == null) return;

        // Hier würden normalerweise Enricher, Redaction und Limiter aufgerufen werden:
        // foreach(var enr in _enrichers) enr.Enrich(evt);
        // _redactionPolicy.Redact(evt);
        // _sizeLimiter.Truncate(evt);

        if (_queue.Writer.TryWrite(evt)) return;
        await HandleBackpressureAsync(evt, ct).ConfigureAwait(false);
    }

    private async ValueTask HandleBackpressureAsync(ILogEvent evt, CancellationToken ct)
    {
        var capacity = Math.Max(1, _options.QueueCapacity);
        var loadMode = _backpressurePolicy.Decide(_queue.Reader.Count, capacity);

        switch (loadMode)
        {
            case DropMode.DropNewest:
                Interlocked.Increment(ref _droppedQueue);
                break;

            case DropMode.DropOldest:
                if (_queue.Reader.TryRead(out _))
                    Interlocked.Increment(ref _droppedQueue);
                else
                {
                    Interlocked.Increment(ref _droppedQueue);
                    return;
                }

                if (!_queue.Writer.TryWrite(evt))
                    Interlocked.Increment(ref _droppedQueue);
                break;

            case DropMode.Block:
                try
                {
                    await _queue.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
                }
                catch
                {
                    Interlocked.Increment(ref _droppedQueue);
                }
                break;

            case DropMode.None:
            default:
                if (!_queue.Writer.TryWrite(evt))
                    Interlocked.Increment(ref _droppedQueue);
                break;
        }
    }

    // NEU: Implementierung von StopAsync mit Drain-Option
    public async Task StopAsync(bool drain, CancellationToken ct)
    {
        // 1. Queue schließen, damit keine neuen Events mehr angenommen werden.
        // Das führt dazu, dass der SenderLoop "zu Ende" läuft, sobald die Queue leer ist.
        _queue.Writer.TryComplete();

        if (!drain)
        {
            // Sofortiger Abbruch: Cancellation Token feuern
            _cts.Cancel();
        }

        // 2. Auf den Sender-Loop warten
        if (_senderTask != null)
        {
            if (drain)
            {
                // Warten, bis der Loop von alleine fertig ist (Queue leer)
                // Optional: Ein Timeout für das Drain setzen, damit wir nicht ewig hängen
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), ct);
                await Task.WhenAny(_senderTask, timeoutTask).ConfigureAwait(false);
            }
            else
            {
                // Bei Sofort-Abbruch nur kurz warten, ob er sauber rauskommt
                try 
                { 
                    await _senderTask.WaitAsync(ct).ConfigureAwait(false); 
                } 
                catch (OperationCanceledException) { /* Expected */ }
            }
        }

        // 3. Transport schließen
        if (_transport != null)
        {
            try { await _transport.CloseAsync(ct).ConfigureAwait(false); } catch { /* Best effort */ }
            
            // Bei Drain noch versuchen, den Transport zu flushen
            if (drain)
            {
                try { await _transport.FlushAsync(ct).ConfigureAwait(false); } catch { /* Best effort */ }
            }
        }
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        // Warten, bis die Queue leer ist (Polling mit Delay ist hier oft das einfachste Mittel)
        while (_queue.Reader.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        if (_transport != null)
            await _transport.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task SenderLoopAsync(CancellationToken ct)
    {
        var transport = _transport ?? throw new InvalidOperationException("Client not started.");
        var batch = new List<ReadOnlyMemory<byte>>(_options.BatchSize);
        var flushInterval = _options.FlushInterval;

        // Wir nutzen WaitToReadAsync, das false zurückgibt, wenn Writer.Complete() gerufen wurde UND Queue leer ist.
        while (await _queue.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (batch.Count < _options.BatchSize && _queue.Reader.TryRead(out var evt))
            {
                var payload = _encoder.Encode(evt);
                batch.Add(payload);
            }

            if (batch.Count > 0)
            {
                foreach (var payload in batch)
                {
                    if (!await transport.TrySendAsync(payload, ct).ConfigureAwait(false))
                    {
                        Interlocked.Increment(ref _droppedTransport);
                    }
                }
                batch.Clear();
                
                // Flush nach jedem Batch oder Intervall? Hier vereinfacht nach Batch.
                // In Hochlast-Szenarien würde man das Entkoppeln (Timer für Flush).
                await transport.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                // Wenn WaitToRead true war, aber TryRead false (Race), kurz warten
                 await Task.Delay(1, ct).ConfigureAwait(false);
            }
        }
        
        // Loop Ende (Queue completed & empty oder Cancelled)
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose macht standardmäßig keinen langen Drain, um Hänger beim GC zu vermeiden.
        await StopAsync(drain: false, CancellationToken.None).ConfigureAwait(false);
        _cts.Dispose();
    }
}