using System.Threading.Channels;

namespace Skynet.Core.Logging;

public sealed class LoggingClient : ILoggingClient, IAsyncDisposable
{
    private readonly IEventTransportFactory _transportFactory;
    private readonly ILogEventEncoder _encoder;
    private readonly IBackpressurePolicy _backpressurePolicy;
    private readonly LoggingClientOptions _options;

    // NEU: Eingefügte Abhängigkeiten
    private readonly IEnumerable<IEnricher>? _enrichers;
    private readonly IRedactionPolicy? _redactionPolicy;
    private readonly ISizeLimiter? _sizeLimiter;

    private readonly Channel<ILogEvent> _queue;
    private readonly CancellationTokenSource _cts = new();

    private IEventTransport? _transport;
    private Task? _senderTask;

    private long _droppedQueue;
    private long _droppedTransport;

    public long DroppedCount => Interlocked.Read(ref _droppedQueue) + Interlocked.Read(ref _droppedTransport);
    public int QueueLength => _queue.Reader.Count;
    public int QueueCapacity => _options.QueueCapacity;

    public LoggingClient(
        IEventTransportFactory transportFactory,
        ILogEventEncoder encoder,
        IBackpressurePolicy backpressurePolicy,
        // Optionale Abhängigkeiten für die Log-Pipeline
        IEnumerable<IEnricher>? enrichers = null,
        IRedactionPolicy? redactionPolicy = null,
        ISizeLimiter? sizeLimiter = null,
        LoggingClientOptions? options = null)
    {
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _backpressurePolicy = backpressurePolicy ?? new SimpleBackpressurePolicy();
        
        _enrichers = enrichers;
        _redactionPolicy = redactionPolicy;
        _sizeLimiter = sizeLimiter;
        
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

    public async ValueTask LogAsync(ILogEvent evt, CancellationToken ct)
    {
        if (evt == null) return;

        // --- NEU: Die Pre-Processing Pipeline ---
        
        // 1. Enrich (z.B. TraceId, AppVersion, UserId hinzufügen)
        if (_enrichers != null)
        {
            foreach (var enr in _enrichers)
            {
                try { enr.Enrich(evt); } catch { /* Enricher sollten nicht crashen, ignorieren */ }
            }
        }

        // 2. Redact (Passwörter, Tokens maskieren)
        if (_redactionPolicy != null)
        {
            try { _redactionPolicy.Redact(evt); } catch { /* Best effort */ }
        }

        // 3. Truncate (Zu lange Strings abschneiden)
        if (_sizeLimiter != null)
        {
            try { _sizeLimiter.Truncate(evt); } catch { /* Best effort */ }
        }

        // -----------------------------------------

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

    public async Task StopAsync(bool drain, CancellationToken ct)
    {
        _queue.Writer.TryComplete();

        if (!drain)
        {
            _cts.Cancel();
        }

        if (_senderTask != null)
        {
            if (drain)
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), ct);
                await Task.WhenAny(_senderTask, timeoutTask).ConfigureAwait(false);
            }
            else
            {
                try 
                { 
                    await _senderTask.WaitAsync(ct).ConfigureAwait(false); 
                } 
                catch (OperationCanceledException) { /* Expected */ }
            }
        }

        if (_transport != null)
        {
            try { await _transport.CloseAsync(ct).ConfigureAwait(false); } catch { /* Best effort */ }
        
            if (drain)
            {
                try { await _transport.FlushAsync(ct).ConfigureAwait(false); } catch { /* Best effort */ }
            }
        }
    }

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
            
                await transport.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                 await Task.Delay(1, ct).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(drain: false, CancellationToken.None).ConfigureAwait(false);
        _cts.Dispose();
    }
}