namespace Skynet.Core.Logging;

using System.Threading.Channels;

/// <summary>
/// Verantwortlich für:
/// - Pufferung und asynchrones Senden von LogEvents.
/// - Nutzung von IBackpressurePolicy zur Entscheidung über Drop/Block-Verhalten.
/// - Batch-Verarbeitung und periodisches Flush.
/// - Integration in lokale InMemory-Transports.
/// </summary>
public sealed class LoggingClient : IAsyncDisposable
{
    private readonly IEventTransportFactory _transportFactory;
    private readonly ILogEventEncoder _encoder;
    private readonly IBackpressurePolicy _backpressurePolicy;
    private readonly LoggingClientOptions _options;

    private readonly Channel<ILogEvent> _queue;
    private readonly CancellationTokenSource _cts = new();

    private IEventTransport? _transport;
    private Task? _senderTask;

    private long _droppedQueue;
    private long _droppedTransport;

    public int InQueueCount => _queue.Reader.Count;
    public long DroppedDueToQueueFull => Interlocked.Read(ref _droppedQueue);
    public long DroppedDueToTransport => Interlocked.Read(ref _droppedTransport);

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
            return;

        var transport = _transportFactory.Create();
        await transport.ConnectAsync(ct).ConfigureAwait(false);

        _transport = transport;
        _senderTask = Task.Run(() => SenderLoopAsync(_cts.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _cts.Cancel();
        _queue.Writer.TryComplete();

        if (_senderTask != null)
            await Task.WhenAny(_senderTask, Task.Delay(2000, ct)).ConfigureAwait(false);

        if (_transport != null)
        {
            try { await _transport.CloseAsync(ct).ConfigureAwait(false); } catch { }
            // Flush nach Close ist nun idempotent und schnell (Buffer bereits geleert)
            try { await _transport.FlushAsync(ct).ConfigureAwait(false); } catch { }
        }
    }


    /// <summary>
    /// Fügt ein Event in die Queue ein (nicht blockierend).
    /// Die BackpressurePolicy entscheidet, ob gedroppt oder blockiert wird.
    /// </summary>
    public bool TryLog(ILogEvent evt)
    {
        if (evt == null) return false;

        // 1) Schnellpfad: passt rein
        if (_queue.Writer.TryWrite(evt))
            return true;

        // 2) Voll: Policy entscheidet
        var capacity = Math.Max(1, _options.QueueCapacity); // defensive
        var loadMode = _backpressurePolicy.Decide(_queue.Reader.Count, capacity);

        switch (loadMode)
        {
            case DropMode.DropNewest:
                // Neuankömmling verwerfen
                Interlocked.Increment(ref _droppedQueue);
                return false;

            case DropMode.DropOldest:
                // ZUERST sicher 1 Element entfernen (wenn möglich) und Drop zählen
                if (_queue.Reader.TryRead(out _))
                {
                    Interlocked.Increment(ref _droppedQueue);
                }
                else
                {
                    // Falls aus Race kein Element gelesen werden konnte -> wie DropNewest behandeln
                    Interlocked.Increment(ref _droppedQueue);
                    return false;
                }

                // Dann den neuen schreiben; wenn das wieder nicht klappt, erneut zählen und false
                if (_queue.Writer.TryWrite(evt))
                    return true;

                Interlocked.Increment(ref _droppedQueue);
                return false;

            case DropMode.Block:
                try
                {
                    // BLOCKIEREND bis Kapazität frei – Achtung im Produktiveinsatz!
                    _queue.Writer.WriteAsync(evt).AsTask().Wait();
                    return true;
                }
                catch
                {
                    Interlocked.Increment(ref _droppedQueue);
                    return false;
                }

            case DropMode.None:
            default:
                // Keine Gegenmaßnahme: wir versuchen EINE letzte, nicht-blockierende Einreihung
                // und geben deren Ergebnis korrekt zurück.
                return _queue.Writer.TryWrite(evt);
        }
    }



    /// <summary>
    /// Wartet, bis Queue und Transport leer sind.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct)
    {
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

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await _queue.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (batch.Count < _options.BatchSize && _queue.Reader.TryRead(out var evt))
                    {
                        var payload = _encoder.Encode(evt);
                        batch.Add(payload);
                    }
                }

                if (batch.Count == 0)
                {
                    await Task.Delay(flushInterval, ct).ConfigureAwait(false);
                    continue;
                }

                foreach (var payload in batch)
                {
                    if (!await transport.TrySendAsync(payload, ct).ConfigureAwait(false))
                    {
                        Interlocked.Increment(ref _droppedTransport);
                    }
                }

                batch.Clear();
                await transport.FlushAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
            }
        }

        // Graceful shutdown: verbleibende Events noch versuchen zu senden
        while (_queue.Reader.TryRead(out var evt))
        {
            var payload = _encoder.Encode(evt);
            var sent = await transport.TrySendAsync(payload, CancellationToken.None).ConfigureAwait(false);
            if (!sent)
                Interlocked.Increment(ref _droppedTransport);
        }

        try { await transport.FlushAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _cts.Dispose();
    }
}
