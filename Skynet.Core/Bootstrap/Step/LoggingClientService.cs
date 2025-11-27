using Skynet.Core.Logging;
using Microsoft.Extensions.Hosting;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Verwaltet den Lebenszyklus des Log-Clients.
/// Sorgt dafür, dass Logs aus der Queue gesendet und beim Shutdown "gedrained" werden.
/// </summary>
public sealed class LoggingClientService : BackgroundService
{
    private readonly ILoggingClient _client;

    public LoggingClientService(ILoggingClient client)
    {
        _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startet den Sender-Loop im Client
        await _client.StartAsync(stoppingToken);

        try { await Task.Delay(-1, stoppingToken); }
        catch (OperationCanceledException) { }

        // WICHTIG: Drain = true sorgt dafür, dass der Client versucht, 
        // den Puffer noch leer zu senden, bevor die App stirbt.
        await _client.StopAsync(drain: true, CancellationToken.None);
    }
}
