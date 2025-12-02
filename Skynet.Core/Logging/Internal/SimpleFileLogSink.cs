namespace Skynet.Core.Logging;

// Eine einfache Sink, die direkt in einen FileStream schreibt.
// Perfekt für Bootstrap oder einfache Log-Szenarien ohne komplexe Rotation.
public sealed class SimpleFileSink : ILogSink
{
    private readonly string _filePath;
    private readonly ILogTextFormatter _formatter;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private FileStream? _fileStream;

    public SimpleFileSink(string filePath, ILogTextFormatter formatter)
    {
        _filePath = filePath;
        _formatter = formatter;
    }

    public async Task WriteAsync(ILogEvent evt, CancellationToken ct)
    {
        var line = _formatter.Format(evt);
        
        // Wir fügen explizit ein NewLine hinzu, da der Formatter keins liefert.
        var bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);

        await _lock.WaitAsync(ct);
        try
        {
            EnsureStreamOpen();
            if (_fileStream != null)
            {
                await _fileStream.WriteAsync(bytes, ct);
                // Im Bootstrap-Modus wollen wir oft sicherstellen, dass es wirklich geschrieben wurde,
                // falls der Prozess crasht. Optional: Flush hier aufrufen.
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
            if (_fileStream != null)
            {
                await _fileStream.FlushAsync(ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task StartAsync(CancellationToken ct)
    {
        // Kann genutzt werden, um die Datei sofort zu öffnen/zu prüfen
        return Task.CompletedTask; 
    }

    private void EnsureStreamOpen()
    {
        if (_fileStream != null) return;

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // FileMode.Append ist wichtig, damit wir bestehende Logs (z.B. vom vorherigen Run) nicht überschreiben,
        // oder wenn wir die Datei im InitStep nur "touch"-ed haben.
        _fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
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
