using System.Collections.Concurrent;

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
    private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _frames;
    private volatile bool _closed;

    public InMemoryEventChannel(IEnumerable<ReadOnlyMemory<byte>> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _frames = new ConcurrentQueue<ReadOnlyMemory<byte>>(frames);
    }

    public Task<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_closed) return Task.FromResult<ReadOnlyMemory<byte>?>(null);

        if (_frames.TryDequeue(out var frame))
            return Task.FromResult<ReadOnlyMemory<byte>?>(frame);

        // nichts mehr vorhanden -> Ende
        _closed = true;
        return Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    public Task CloseAsync(CancellationToken ct)
    {
        _closed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _closed = true;
        return ValueTask.CompletedTask;
    }
}
