// Skynet.Core/Logging/CoreFileLoggerProvider.cs

using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

internal sealed class BootstrapFileLoggerProvider : ILoggerProvider
{
    private readonly LogWriter _writer;
    private bool _disposed;

    public BootstrapFileLoggerProvider(BootstrapFileLoggerOptions options)
    {
        _writer = new LogWriter(options);
    }

    public ILogger CreateLogger(string categoryName) => new BootstrapFileLogger(categoryName, _writer);

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Dispose();
    }
}

internal sealed class LogWriter : ILogWriter, IDisposable
{
    private readonly BootstrapFileLoggerOptions _options;
    private readonly Channel<string> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly object _fileLock = new();

    private FileStream? _stream;
    private string _currentFilePath = string.Empty;

    public LogWriter(BootstrapFileLoggerOptions options)
    {
        _options = options;
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });

        if (_options.Enabled)
        {
            Directory.CreateDirectory(_options.DirectoryPath);
            OpenCurrentFile();
            _worker = Task.Run(WorkerLoop);
        }
        else
        {
            _worker = Task.CompletedTask;
        }
    }

    public void Post(string line)
    {
        if (!_options.Enabled) return;
        _channel.Writer.TryWrite(line);
    }

    // Neu: expliziter Flush (drain + stream flush)
    public void Flush()
    {
        try
        {
            // so viel wie möglich aus dem Channel ziehen und schreiben
            var sb = new StringBuilder(8 * 1024);
            while (_channel.Reader.TryRead(out var line))
            {
                sb.Append(line);
                if (sb.Length >= 8 * 1024)
                {
                    TryWrite(sb);
                    sb.Clear();
                }
            }
            if (sb.Length > 0) TryWrite(sb);
        }
        catch { /* ignore */ }

        lock (_fileLock)
        {
            try { _stream?.Flush(); } catch { /* ignore */ }
        }

        void TryWrite(StringBuilder s)
        {
            try { WriteToFile(s); } catch { /* ignore */ }
        }
    }

    public void Dispose()
    {
        // 1) keine neuen Einträge mehr
        _channel.Writer.TryComplete();

        // 2) Worker beenden lassen
        try { _worker.Wait(TimeSpan.FromSeconds(3)); } catch { /* ignore */ }

        // 3) letzte Reste sicher flushen
        try { Flush(); } catch { /* ignore */ }

        // 4) Stream schließen
        lock (_fileLock)
        {
            _stream?.Dispose();
            _stream = null;
        }

        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task WorkerLoop()
    {
        var token = _cts.Token;
        var flushInterval = _options.FlushInterval;
        var sb = new StringBuilder(16 * 1024);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                var nextFlushAt = DateTime.UtcNow + flushInterval;

                while (DateTime.UtcNow < nextFlushAt && _channel.Reader.TryRead(out var line))
                {
                    sb.Append(line);
                }

                if (sb.Length > 0)
                {
                    try { WriteToFile(sb); } catch { /* ignore */ }
                    sb.Clear();
                }

                await Task.Delay(10, token).ConfigureAwait(false);

                TryRotateIfNeeded();
                TryCleanupRetention();
            }

            // Channel ist abgeschlossen → Rest entleeren
            while (_channel.Reader.TryRead(out var line2))
                sb.Append(line2);

            if (sb.Length > 0)
            {
                try { WriteToFile(sb); } catch { /* ignore */ }
                sb.Clear();
            }

            // finaler Flush
            lock (_fileLock)
            {
                try { _stream?.Flush(); } catch { /* ignore */ }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch
        {
            // ignore
        }
    }

    private void WriteToFile(StringBuilder sb)
    {
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        lock (_fileLock)
        {
            if (_stream == null)
            {
                OpenCurrentFile();
                if (_stream == null) return;
            }

            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }
    }

    private void OpenCurrentFile()
    {
        var baseName = $"{_options.FileNamePrefix}-{DateTime.UtcNow:yyyyMMdd}.log";
        _currentFilePath = Path.Combine(_options.DirectoryPath, baseName);

        _stream = new FileStream(
            _currentFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
    }

    private void TryRotateIfNeeded()
    {
        try
        {
            FileInfo fi;
            lock (_fileLock)
            {
                if (string.IsNullOrEmpty(_currentFilePath) || _stream == null) return;
                _stream.Flush();
                fi = new FileInfo(_currentFilePath);
            }

            if (!fi.Exists || fi.Length < _options.MaxFileSizeBytes) return;

            lock (_fileLock)
            {
                _stream?.Dispose();
                _stream = null;

                var i = 1;
                string rotated;
                do
                {
                    rotated = $"{_currentFilePath}.{i}";
                    i++;
                } while (File.Exists(rotated));

                File.Move(_currentFilePath, rotated);
                OpenCurrentFile();
            }
        }
        catch
        {
            // ignored
        }
    }

    private void TryCleanupRetention()
    {
        try
        {
            if (_options.RetentionDays <= 0) return;

            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
            var files = Directory.GetFiles(_options.DirectoryPath, $"{_options.FileNamePrefix}-*.log*");

            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    if (fi.LastWriteTimeUtc < cutoff)
                    {
                        lock (_fileLock)
                        {
                            if (!string.Equals(f, _currentFilePath, StringComparison.OrdinalIgnoreCase))
                                File.Delete(f);
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch
        {
            // ignored
        }
    }
}
