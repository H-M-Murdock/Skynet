namespace Skynet.Core.Logging;

// Orchestriert Listener/Channels, decodiert Events und dispatcht an Router/Sinks.
public interface ILoggingServer
{
    // Startet Accept-/Read-/Dispatch-Loop (im Hintergrund).
    Task StartAsync(CancellationToken ct);

    // Stoppt sauber: Verbindungen schließen, Sinks flushen.
    Task StopAsync(CancellationToken ct);
}
