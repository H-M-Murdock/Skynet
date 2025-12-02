using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

public sealed class BootstrapLoggerProvider(ILogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new BootstrapLogger(categoryName, sink);
    }

    public void Dispose()
    {
        // Sink wird hier nicht disposed, da sie evtl. vom BootstrapContext
        // weitergereicht werden soll. Falls sie nur hier lebt, müsste man Dispose aufrufen.
        // Da ILogSink IAsyncDisposable ist, ist synchrones Dispose hier knifflig.
        // Wir überlassen den Lifecycle dem BootstrapContext.
    }
}