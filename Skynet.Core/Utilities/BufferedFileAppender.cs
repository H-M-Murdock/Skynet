using System.Text;

namespace Skynet.Core;

/// <summary>
/// Streaming-File-Appender mit internem String-Puffer und optionaler Größenrotation.
/// - Öffnet FileStream im Append-Modus (Share.Read).
/// - Append() puffert Zeilen; FlushAsync schreibt UTF-8.
/// - Rotation: bei Überschreiten von maxBytes wird die aktuelle Datei geschlossen und als .1, .2, ... rotiert.
/// - WriteThrough: Flush nach jedem Write.
/// Thread-sicher.
/// </summary>
public sealed class BufferedFileAppender : IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly StringBuilder _buffer = new(8 * 1024);

    private readonly bool _writeThrough;
    private FileStream? _stream;
    private string? _currentFullPath;
    private long _currentLength;

    public BufferedFileAppender(bool writeThrough = false)
    {
        _writeThrough = writeThrough;
    }

    public int BufferedBytes
    {
        get { lock (_lock) return _buffer.Length; }
    }

    public void Append(string text)
    {
        if (text is null) return;
        lock (_lock) { _buffer.Append(text); }
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        string? toWrite;
        lock (_lock)
        {
            if (_buffer.Length == 0) return;
            toWrite = _buffer.ToString();
            _buffer.Clear();
        }

        if (string.IsNullOrEmpty(toWrite)) return;

        var bytes = Encoding.UTF8.GetBytes(toWrite);
        await EnsureStreamAsync(ct).ConfigureAwait(false);
        await _stream!.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        _currentLength += bytes.Length;

        if (_writeThrough)
            await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public async Task EnsureOpenedForAsync(string fullPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) throw new ArgumentNullException(nameof(fullPath));
        if (string.Equals(_currentFullPath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            await Task.CompletedTask;
            return;
        }

        await CloseStreamAsync().ConfigureAwait(false);
        await OpenStreamAsync(fullPath, ct).ConfigureAwait(false);
    }

    public long CurrentLength
    {
        get { lock (_lock) return _currentLength; }
    }

    public string? CurrentFullPath
    {
        get { lock (_lock) return _currentFullPath; }
    }

    public async Task RotateToAsync(string newFullPath, CancellationToken ct)
    {
        // Schließen, existierende Datei ggf. umbenennen, neue öffnen
        await CloseStreamAsync().ConfigureAwait(false);

        // Falls die Zielbasis-Datei existiert, rotiere sie mit Suffix .1, .2, ...
        if (File.Exists(newFullPath))
        {
            var i = 1;
            string rotated;
            do
            {
                rotated = newFullPath + "." + i.ToString();
                i++;
            } while (File.Exists(rotated));

            try { File.Move(newFullPath, rotated); } catch { /* ignore */ }
        }

        await OpenStreamAsync(newFullPath, ct).ConfigureAwait(false);
    }

    private async Task EnsureStreamAsync(CancellationToken ct)
    {
        if (_stream is not null) return;
        if (string.IsNullOrEmpty(_currentFullPath))
            throw new InvalidOperationException("Appender has no current path. Call EnsureOpenedForAsync first.");
        await OpenStreamAsync(_currentFullPath!, ct).ConfigureAwait(false);
    }

    private async Task OpenStreamAsync(string fullPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var fileOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;
        if (_writeThrough) fileOptions |= FileOptions.WriteThrough;

        var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read, 128 * 1024, fileOptions);
        var fi = new FileInfo(fullPath);

        lock (_lock)
        {
            _stream = stream;
            _currentFullPath = fullPath;
            _currentLength = fi.Exists ? fi.Length : 0L;
        }

        await Task.CompletedTask;
    }

    private async Task CloseStreamAsync()
    {
        FileStream? s;
        lock (_lock)
        {
            s = _stream;
            _stream = null;
            _currentFullPath = null;
            _currentLength = 0;
        }
        try { s?.Dispose(); } catch { /* ignore */ }
        await Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            try { _stream?.Dispose(); } catch { /* ignore */ }
            _stream = null;
            _currentFullPath = null;
            _currentLength = 0;
        }
        return ValueTask.CompletedTask;
    }
}
