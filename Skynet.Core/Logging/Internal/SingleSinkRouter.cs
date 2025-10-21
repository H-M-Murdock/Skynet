using System;

namespace Skynet.Core.Logging;

/// <summary>
/// Minimaler Router: gibt immer den gleichen Sink zurück.
/// Nützlich für Bootstrapping/Tests oder als Default.
/// </summary>
public sealed class SingleSinkRouter : ILogRouter, IAsyncDisposable
{
    private readonly ILogSink _sink;

    public SingleSinkRouter(ILogSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public ILogSink Resolve(ILogEvent evt) => _sink;

    public async ValueTask DisposeAsync()
    {
        await _sink.DisposeAsync().ConfigureAwait(false);
    }
}
