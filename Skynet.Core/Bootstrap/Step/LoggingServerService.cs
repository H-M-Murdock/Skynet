using Skynet.Core.Logging;
using Microsoft.Extensions.Hosting;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Verwaltet den Lebenszyklus des Log-Servers.
/// Wird NUR registriert, wenn diese Instanz auch Logs empfangen und schreiben soll.
/// </summary>
public sealed class LoggingServerService : BackgroundService
{
    private readonly ILoggingServer _server;
    private readonly ILogSink _sink;

    public LoggingServerService(ILoggingServer server, FileLogSink sink)
    {
        _server = server;
        _sink = sink;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Infrastruktur hochfahren
        await _sink.StartAsync(stoppingToken);
        await _server.StartAsync(stoppingToken);

        // 2. Warten
        try { await Task.Delay(-1, stoppingToken); }
        catch (OperationCanceledException) { }

        // 3. Sauber herunterfahren
        await _server.StopAsync(CancellationToken.None);
        await _sink.FlushAsync(CancellationToken.None);
    }
}
