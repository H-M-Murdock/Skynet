using System.Text;

using System.Text;

namespace Skynet.Core.Logging;

/// <summary>
/// Ein einfacher File-Sink für den Bootstrap.
/// - Schreibt stur (Append) in die angegebene Datei.
/// - Führt beim Start zwingend eine Retention im angegebenen Ordner aus.
/// </summary>
public sealed class SimpleFileSink : ILogSink
{
    private readonly string _targetFilePath;
    private readonly string _retentionRootPath;
    private readonly string _retentionSearchPattern;
    
    private readonly ILogTextFormatter _formatter;
    private readonly IFileRetentionPolicy _retentionPolicy;
    
    private readonly SemaphoreSlim _lock = new(1, 1);
    private FileStream? _fileStream;

    /// <summary>
    /// Erstellt den Sink.
    /// </summary>
    /// <param name="targetFilePath">Die Datei, in die aktuell geschrieben wird (z.B. .../bootstrap-2023-12-01.log).</param>
    /// <param name="retentionRootPath">Der Ordner, in dem aufgeräumt werden soll (z.B. .../logs).</param>
    /// <param name="retentionSearchPattern">Das Muster für alte Dateien (z.B. bootstrap-*.log).</param>
    /// <param name="formatter">Der Text-Formatter.</param>
    /// <param name="retentionPolicy">Die Retention-Logik (Pflicht).</param>
    public SimpleFileSink(
        string targetFilePath,
        string retentionRootPath,
        string retentionSearchPattern,
        ILogTextFormatter formatter,
        IFileRetentionPolicy retentionPolicy)
    {
        if (string.IsNullOrWhiteSpace(targetFilePath)) throw new ArgumentNullException(nameof(targetFilePath));
        if (string.IsNullOrWhiteSpace(retentionRootPath)) throw new ArgumentNullException(nameof(retentionRootPath));
        if (string.IsNullOrWhiteSpace(retentionSearchPattern)) throw new ArgumentNullException(nameof(retentionSearchPattern));

        _targetFilePath = targetFilePath;
        _retentionRootPath = retentionRootPath;
        _retentionSearchPattern = retentionSearchPattern;
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _retentionPolicy = retentionPolicy ?? throw new ArgumentNullException(nameof(retentionPolicy));
    }

    public async Task WriteAsync(ILogEvent evt, CancellationToken ct)
    {
        var line = _formatter.Format(evt);
        var bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);

        await _lock.WaitAsync(ct);
        try
        {
            EnsureStreamOpen();
            if (_fileStream != null)
            {
                await _fileStream.WriteAsync(bytes, ct);
                await _fileStream.FlushAsync(ct); 
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_fileStream != null) await _fileStream.FlushAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task StartAsync(CancellationToken ct)
    {
        _retentionPolicy.Apply(_retentionRootPath, _retentionSearchPattern);
        return Task.CompletedTask; 
    }

    private void EnsureStreamOpen()
    {
        if (_fileStream != null) return;

        var dir = Path.GetDirectoryName(_targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _fileStream = new FileStream(_targetFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
    }

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_fileStream != null)
            {
                await _fileStream.DisposeAsync();
                _fileStream = null;
            }
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
