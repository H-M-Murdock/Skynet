using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

/// <summary>
/// Produktionsfähiger Logging-Server, der eingehende Verbindungen (\see IEventListener)
/// akzeptiert, Frames liest (\see IEventChannel.ReadAsync), Events decodiert (\see ILogEventEncoder)
/// und über einen Router (\see ILogRouter) zu Sinks dispatcht.
///
/// Schwerpunkte:
/// - Keine Blockier-/Deadlock-Risiken: zentrale bounded Channel-Queue schützt vor OOM.
/// - Backpressure/Drop-Strategie konfigurierbar.
/// - Saubere Beendigung (StopAsync) mit finalem Flush aller bekannten Sinks.
/// - Memory-Leak-Schutz: Reader-Tasks reinigen sich selbst nach Abschluss.
///
/// Fehlerstrategie:
/// - Exceptions in Readern/Workern werden geloggt und führen nicht zum Prozessabbruch.
/// - Ungültige Frames werden verworfen und gezählt.
/// </summary>
public sealed class LoggingServer : ILoggingServer, IAsyncDisposable
{
    private readonly IEventListener _listener;
    private readonly ILogEventEncoder _encoder;
    private readonly ILogRouter _router;
    private readonly ILogEventMaterializer _materializer;
    private readonly IEnumerable<IEnricher>? _enrichers;
    private readonly ILogger<LoggingServer>? _logger;
    private readonly LoggingServerOptions _options;

    private readonly Channel<ReadOnlyMemory<byte>> _dispatch;
    private readonly CancellationTokenSource _cts = new();

    private Task? _acceptLoop;
    
    // FIX: Dictionary statt Bag, damit wir Tasks gezielt entfernen können (Memory Leak Fix)
    private readonly ConcurrentDictionary<int, Task> _readerTasks = new();
    
    private readonly List<Task> _workers = new();
    private Task? _flushLoop;

    private volatile int _started; // 0/1
    private volatile int _stopped; // 0/1

    // Alle Sinks, die während der Laufzeit aufgetreten sind (für Stop/Flush).
    private readonly ConcurrentDictionary<ILogSink, byte> _knownSinks = new();

    // Zähler/Telemetry (einfach gehalten; ggf. durch Meter/Counter ersetzen)
    public long DroppedFrames => _droppedFrames;
    public long DecodingErrors => _decodingErrors;
    private long _droppedFrames;
    private long _decodingErrors;

    public LoggingServer(
        IEventListener listener,
        ILogEventEncoder encoder,
        ILogRouter router,
        ILogEventMaterializer materializer,
        LoggingServerOptions? options = null,
        IEnumerable<IEnricher>? enrichers = null,
        ILogger<LoggingServer>? logger = null
        )
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _materializer = materializer;
        _enrichers = enrichers;
        _logger = logger;
        _options = options ?? new LoggingServerOptions();

        var bounded = new BoundedChannelOptions(_options.MaxQueueLength)
        {
            AllowSynchronousContinuations = false,
            SingleReader = false,
            SingleWriter = false,
            // Drop/Wait-Strategie: produktiv oft "Wait"; hier konfigurierbar.
            FullMode = _options.QueueFullMode
        };
        _dispatch = Channel.CreateBounded<ReadOnlyMemory<byte>>(bounded);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return; // idempotent

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var token = linked.Token;

        _logger?.LogInformation("LoggingServer Start: Encoder={ContentType}@{Version}, Workers={Workers}, MaxQueue={MaxQueue}",
            _encoder.ContentType, _encoder.Version, _options.WorkerCount, _options.MaxQueueLength);

        // Bind Listener
        await _listener.BindAsync(token).ConfigureAwait(false);

        _acceptLoop = Task.Run(() => AcceptLoopAsync(token), token);

        for (int i = 0; i < _options.WorkerCount; i++)
        {
            var worker = Task.Run(() => WorkerLoopAsync(token), token);
            _workers.Add(worker);
        }

        if (_options.PeriodicFlushInterval > TimeSpan.Zero)
        {
            _flushLoop = Task.Run(() => FlushLoopAsync(token), token);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
            return; // idempotent

        _logger?.LogInformation("LoggingServer Stop initiated.");

        using var stopTimeoutCts = new CancellationTokenSource(_options.StopTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, stopTimeoutCts.Token);
        var overallStopToken = linked.Token;

        // 1. Listener schließen (stoppt AcceptLoop)
        try { await _listener.CloseAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { _logger?.LogWarning(ex, "Listener.CloseAsync threw."); }
    
        await SafeWaitAsync(_acceptLoop, overallStopToken, nameof(_acceptLoop));

        // 2. Warten auf aktive Reader
        // FIX: Dictionary Values abrufen (Snapshot)
        await SafeWhenAllAsync(_readerTasks.Values.ToArray(), overallStopToken, "reader");

        // 3. Queue schließen
        _dispatch.Writer.TryComplete();

        // 4. Warten auf Worker
        await SafeWhenAllAsync(_workers.ToArray(), overallStopToken, "workers");

        // 5. Globales Cancel
        _cts.Cancel();

        await SafeWaitAsync(_flushLoop, overallStopToken, nameof(_flushLoop));

        // 6. Finaler Flush
        await FlushAllSinksAsync(CancellationToken.None).ConfigureAwait(false);

        _logger?.LogInformation("LoggingServer stopped. DroppedFrames={Dropped}, DecodeErrors={DecodeErrors}", DroppedFrames, DecodingErrors);
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(CancellationToken.None); } catch { /* best-effort */ }
        _cts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ch = await _listener.AcceptAsync(ct).ConfigureAwait(false);
                
                // FIX: Task erstellen, tracken und Auto-Cleanup einhängen
                var readerTask = Task.Run(() => ReaderLoopAsync(ch, ct), ct);
                
                // Task im Dictionary registrieren
                _readerTasks.TryAdd(readerTask.Id, readerTask);

                // Wenn der Task fertig ist (egal ob Erfolg oder Fehler), entferne ihn aus dem Dictionary
                _ = readerTask.ContinueWith(t => 
                {
                    _readerTasks.TryRemove(t.Id, out _);
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (InvalidOperationException inv) when (inv.Message.Contains("No pending channels", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(_options.AcceptBackoff, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AcceptAsync failed");
                await Task.Delay(_options.AcceptBackoff, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ReaderLoopAsync(IEventChannel ch, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ReadOnlyMemory<byte>? frame;
                try
                {
                    frame = await ch.ReadAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                if (frame is null) break; 

                if (_options.QueueFullMode == BoundedChannelFullMode.Wait)
                {
                    await _dispatch.Writer.WriteAsync(frame.Value, ct).ConfigureAwait(false);
                }
                else
                {
                    if (!_dispatch.Writer.TryWrite(frame.Value))
                    {
                        Interlocked.Increment(ref _droppedFrames);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Exceptions im Reader sollen den Server nicht crashen, nur den einen Channel schließen
            _logger?.LogWarning(ex, "ReaderLoop error.");
        }
        finally
        {
            try { await ch.CloseAsync(CancellationToken.None).ConfigureAwait(false); }
            catch { /* ignore */ }
            await ch.DisposeAsync();
        }
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        var reader = _dispatch.Reader;
        while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (reader.TryRead(out var payload))
            {
                try
                {
                    if (!_encoder.TryDecode(payload.Span, out var decoded) || decoded is null)
                    {
                        Interlocked.Increment(ref _decodingErrors);
                        continue;
                    }

                    var evt = _materializer.Materialize(decoded); 
                    
                    if (_enrichers is not null)
                    {
                        foreach (var e in _enrichers)
                        {
                            try { e.Enrich(evt); } catch (Exception enrEx) { _logger?.LogWarning(enrEx, "Enricher failed."); }
                        }
                    }
                
                    var sink = _router.Resolve(evt);
                    _knownSinks.TryAdd(sink, 0);

                    await sink.WriteAsync(evt, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Dispatch failed");
                }
            }
        }
    }

    private async Task FlushLoopAsync(CancellationToken ct)
    {
        var interval = _options.PeriodicFlushInterval;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
                await FlushAllSinksAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger?.LogWarning(ex, "Periodic flush failed."); }
        }
    }

    private async Task FlushAllSinksAsync(CancellationToken ct)
    {
        foreach (var sink in _knownSinks.Keys)
        {
            try { await sink.FlushAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* stop */ }
            catch (Exception ex) { _logger?.LogWarning(ex, "Sink flush failed."); }
        }
    }

    private static async Task SafeWhenAllAsync(Task[] tasks, CancellationToken ct, string name)
    {
        if (tasks.Length == 0) return;
        try { await Task.WhenAll(tasks).WaitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception) { /* aggregated already surfaced in logs; ignore */ }
    }

    private static async Task SafeWaitAsync(Task? task, CancellationToken ct, string name)
    {
        if (task is null) return;
        try { await task.WaitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception) { /* surfaced in logs; ignore */ }
    }
}