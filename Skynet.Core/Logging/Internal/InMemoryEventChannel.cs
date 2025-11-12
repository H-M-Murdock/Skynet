using System.Threading.Channels;

namespace Skynet.Core.Logging;

/// <summary>
/// Einfache In-Memory-Implementierung von IEventChannel für Tests:
/// - Frames werden vorab injiziert (Constructor).
/// - ReadAsync liefert sequenziell Frames, anschließend null.
/// - CloseAsync markiert das Ende vorzeitig.
/// Thread-sicher.
/// </summary>
public sealed class InMemoryEventChannel : IEventChannel
{
    private readonly Channel<ReadOnlyMemory<byte>> _channel;

    public InMemoryEventChannel(int capacity)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropWrite // Oder eine andere passende Strategie
        };
        _channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(options);
    }
    
    public InMemoryEventChannel(IEnumerable<ReadOnlyMemory<byte>> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var frameList = frames as IReadOnlyCollection<ReadOnlyMemory<byte>> ?? frames.ToList();
        var options = new BoundedChannelOptions(frameList.Count > 0 ? frameList.Count : 1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropWrite
        };
        _channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(options);

        foreach (var frame in frameList)
        {
            _channel.Writer.TryWrite(frame);
        }

        _channel.Writer.TryComplete();
    }

    /// <summary>
    /// Wird vom InMemoryEventTransport aufgerufen, um Daten in den Kanal zu schreiben.
    /// </summary>
    public bool TryWrite(ReadOnlyMemory<byte> frame)
    {
        return _channel.Writer.TryWrite(frame);
    }

    /// <summary>
    /// Wird vom LoggingServer aufgerufen, um Daten aus dem Kanal zu lesen.
    /// </summary>
    public async Task<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken ct)
    {
        try
        {
            // Wartet, bis ein Element verfügbar ist oder der Channel geschlossen wurde.
            // Gibt dann das Element zurück.
            return await _channel.Reader.ReadAsync(ct);
        }
        catch (ChannelClosedException)
        {
            // Dies ist das korrekte, erwartete Signal für "End of File" (EOF).
            return null;
        }
    }

    /// <summary>
    /// Schließt den Kanal für weitere Schreibvorgänge. Der Reader kann den Puffer
    /// danach noch vollständig leeren.
    /// </summary>
    public Task CloseAsync(CancellationToken ct)
    {
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
