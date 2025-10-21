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

    private readonly string _baseRoot;
    private readonly string _tenant;
    private readonly string? _subFolder;
    private readonly string _key;
    private readonly bool _writeThrough;
    private readonly long _maxBytes; // 0 = keine Rotation

    private FileStream? _stream;
    private string? _currentFullPath;
    private long _currentLength;

    public BufferedFileAppender(string baseRoot, string tenant, string key, string? subFolder = null, bool writeThrough = false, long maxBytes = 0)
    {
        if (string.IsNullOrWhiteSpace(baseRoot)) throw new ArgumentNullException(nameof(baseRoot));
        if (string.IsNullOrWhiteSpace(tenant)) throw new ArgumentNullException(nameof(tenant));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

        _baseRoot = baseRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _tenant = tenant;
        _subFolder = subFolder;
        _key = key;
        _writeThrough = writeThrough;
        _maxBytes = maxBytes;
    }

    public int BufferedBytes
    {
        get { lock (_lock) return _buffer.Length; }
    }

    public void Append(string text)
    {
        if (text is null) return;
        lock (_lock)
        {
            _buffer.Append(text);
        }
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

        await EnsureStreamAsync(ct).ConfigureAwait(false);

        // Rotation vor dem Schreiben prüfen
        if (_maxBytes > 0 && (_currentLength + toWrite.Length) > _maxBytes)
        {
            await RotateAsync(ct).ConfigureAwait(false);
        }

        var bytes = Encoding.UTF8.GetBytes(toWrite);
        await _stream!.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        _currentLength += bytes.Length;

        if (_writeThrough)
        {
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task EnsureStreamAsync(CancellationToken ct)
    {
        if (_stream is not null) return;

        var full = IoUtilities.BuildSafeFullPath(_baseRoot, _tenant, _key, _subFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        var fileOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;
        if (_writeThrough) fileOptions |= FileOptions.WriteThrough;

        var stream = new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read, 128 * 1024, fileOptions);
        FileInfo fi;
        try { fi = new FileInfo(full); }
        catch { fi = new FileInfo(full); }

        lock (_lock)
        {
            _stream = stream;
            _currentFullPath = full;
            _currentLength = fi.Exists ? fi.Length : 0L;
        }

        await Task.CompletedTask;
    }

    private async Task RotateAsync(CancellationToken ct)
    {
        FileStream? toDispose;
        string? path;
        lock (_lock)
        {
            toDispose = _stream;
            path = _currentFullPath;
            _stream = null;
            _currentFullPath = null;
            _currentLength = 0;
        }

        try { toDispose?.Dispose(); } catch { /* ignore */ }
        if (string.IsNullOrEmpty(path)) return;

        // finde nächste freie Suffix-Datei
        var i = 1;
        string rotated;
        do
        {
            rotated = path + "." + i.ToString();
            i++;
        } while (File.Exists(rotated));

        try
        {
            File.Move(path, rotated);
        }
        catch
        {
            // wenn Move fehlschlägt, einfach ohne Rotation weiter
        }

        await EnsureStreamAsync(ct).ConfigureAwait(false);
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
