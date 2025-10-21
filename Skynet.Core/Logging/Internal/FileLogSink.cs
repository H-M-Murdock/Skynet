using System.Collections.Concurrent;
using System.Text;
using Skynet.Core;

namespace Skynet.Core.Logging;

/// <summary>
/// Streaming-basierter File-Sink:
/// - Pro Ziel-Key (gerendert aus Template) ein Writer mit Append-Stream und Puffer.
/// - Periodischer Flush (Timer) und Schwellwert-Flush (BufferFlushBytes).
/// - Größenbasierte Rotation: bei Überschreitung von MaxFileBytes wird die aktuelle Datei rotiert (Suffix .1, .2, ...).
/// - Retention: löscht alte Dateien (RetentionDays) und begrenzt Gesamtgröße (MaxDirectoryBytesApprox) im Log-Baum.
/// - Thread-sicher; keine Ganzdatei-Rewrites.
/// Hinweise:
/// - Für tagesbasierte Dateien Template wie "{yyyy-MM-dd}/{level}.log" verwenden.
/// - Rotation prüft vor jedem Flush sowie zeitgesteuert.
/// </summary>
public sealed class FileLogSink : ILogSink
{
    private readonly ILogTextFormatter _formatter;
    private readonly IPathTemplateRenderer _renderer;
    private FileLogSinkOptions _options;
    private readonly IDisposable? _reload;

    private readonly ConcurrentDictionary<string, BufferedFileAppender> _appenders = new(StringComparer.Ordinal);
    private readonly object _timerLock = new();
    private System.Threading.Timer? _flushTimer;
    private System.Threading.Timer? _retentionTimer;
    private volatile bool _started;

    public FileLogSink(ILogTextFormatter formatter, IPathTemplateRenderer renderer, Microsoft.Extensions.Options.IOptionsMonitor<FileLogSinkOptions> options)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _options = options?.CurrentValue ?? new FileLogSinkOptions();
        _reload = options?.OnChange((o, _) => _options = o ?? new FileLogSinkOptions());
    }

    public Task StartAsync(CancellationToken ct)
    {
        if (_started) return Task.CompletedTask;
        _started = true;

        lock (_timerLock)
        {
            _flushTimer?.Dispose();
            if (_options.FlushIntervalMs > 0)
            {
                _flushTimer = new System.Threading.Timer(
                    _ => { _ = SafeFlushAllAsync(); },
                    null,
                    _options.FlushIntervalMs,
                    _options.FlushIntervalMs);
            }

            _retentionTimer?.Dispose();
            if (_options.RetentionCheckIntervalMs > 0)
            {
                _retentionTimer = new System.Threading.Timer(
                    _ => SafeRetentionSweep(),
                    null,
                    _options.RetentionCheckIntervalMs,
                    _options.RetentionCheckIntervalMs);
            }
        }

        return Task.CompletedTask;
    }

    public Task WriteAsync(ILogEvent evt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var now = _options.UseEventTimestampForPath ? evt.Timestamp : DateTimeOffset.UtcNow;

        // Template-Anwendung:
        // Falls der injizierte Renderer ein DefaultPathTemplateRenderer ist, nutzen wir die 3-Param-Variante,
        // sonst die 2-Param-Signatur.
        string key;
        if (_renderer is DefaultPathTemplateRenderer defRenderer)
        {
            key = defRenderer.Render(evt, now, _options.PathTemplate);
        }
        else
        {
            key = _renderer.Render(evt, now);
        }

        if (string.IsNullOrWhiteSpace(key)) return Task.CompletedTask;

        var line = _formatter.Format(evt) + Environment.NewLine;

        var baseRoot = _options.BaseRoot ?? AppContext.BaseDirectory!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var tenant = _options.Tenant ?? "logs";
        var sub = _options.SubFolder;

        var app = _appenders.GetOrAdd(key, _ => new BufferedFileAppender(
            baseRoot: baseRoot,
            tenant: tenant,
            key: key,
            subFolder: sub,
            writeThrough: _options.WriteThrough,
            maxBytes: _options.MaxFileBytes));

        app.Append(line);

        if (_options.BufferFlushBytes > 0 && app.BufferedBytes >= _options.BufferFlushBytes)
        {
            _ = app.FlushAsync(ct);
        }

        return Task.CompletedTask;
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        foreach (var kv in _appenders)
        {
            ct.ThrowIfCancellationRequested();
            await kv.Value.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_timerLock)
        {
            _flushTimer?.Dispose();
            _flushTimer = null;
            _retentionTimer?.Dispose();
            _retentionTimer = null;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await FlushAsync(cts.Token).ConfigureAwait(false);
        }
        catch { /* best effort */ }

        foreach (var kv in _appenders)
        {
            try { await kv.Value.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
        }
        _appenders.Clear();
        _reload?.Dispose();
    }

    private async Task SafeFlushAllAsync()
    {
        try { await FlushAsync(CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
    }

    private void SafeRetentionSweep()
    {
        try { RunRetentionSweep(); } catch { /* ignore */ }
    }

    private void RunRetentionSweep()
    {
        var baseRoot = _options.BaseRoot ?? AppContext.BaseDirectory!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var tenant = _options.Tenant ?? "logs";
        var sub = _options.SubFolder;

        string root;
        try
        {
            // "." als key, damit BuildSafeFullPath uns den Ordner resolved
            root = IoUtilities.BuildSafeFullPath(baseRoot, tenant, ".", sub);
        }
        catch { return; }

        if (!Directory.Exists(root)) return;

        // 1) Alter-basierte Löschung
        if (_options.RetentionDays > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
            foreach (var file in EnumerateFilesSafe(root))
            {
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.LastWriteTimeUtc < cutoff)
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                }
                catch { /* ignore */ }
            }
        }

        // 2) Größen-Cap
        if (_options.MaxDirectoryBytesApprox > 0)
        {
            var files = EnumerateFilesSafe(root).Select(p =>
            {
                try { var fi = new FileInfo(p); return (Path: p, Size: fi.Length, Last: fi.LastWriteTimeUtc); }
                catch { return (Path: p, Size: 0L, Last: DateTime.MinValue); }
            })
            .Where(x => !string.IsNullOrEmpty(x.Path))
            .OrderByDescending(x => x.Last) // neueste zuerst
            .ToList();

            long total = files.Sum(x => x.Size);
            for (int i = files.Count - 1; i >= 0 && total > _options.MaxDirectoryBytesApprox; i--)
            {
                var f = files[i];
                try
                {
                    File.SetAttributes(f.Path, FileAttributes.Normal);
                    File.Delete(f.Path);
                    total -= f.Size;
                }
                catch { /* ignore */ }
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        try { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
        catch { return Array.Empty<string>(); }
    }

    
}