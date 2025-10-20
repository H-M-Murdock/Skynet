namespace Skynet.Core.Logging;

// Orchestriert Listener/Channels, decodiert Events und dispatcht an Router/Sinks.
public interface ILoggingServer
{
    // Startet Accept-/Read-/Dispatch-Loop (im Hintergrund).
    // Konzept:
    // - Paralleles Accept: ein Accept-Loop akzeptiert kontinuierlich IEventChannel-Verbindungen.
    // - Für jede Verbindung: ein Lese-Task, der Frames liest und in eine zentrale, begrenzte Dispatch-Queue legt.
    // - Bounded Dispatch-Queue: schützt vor OOM; bei Überlast werden Frames verworfen oder Backpressure angewandt.
    // - Worker-Pool: mehrere Dispatch-Worker de-queuen Frames, decodieren (ILogEventEncoder.TryDecode) und routen zu Sinks.
    // - Batch-Flush: Worker oder separater Timer flushen periodisch alle aktiven Sinks (z. B. Intervall-basiert).
    Task StartAsync(CancellationToken ct);

    // Stoppt sauber:
    // - Stoppt Accept (IEventListener.CloseAsync) und verhindert neue Verbindungen.
    // - Lässt vorhandene Reader-Tasks auslaufen oder bricht sie nach Timeout ab.
    // - Drained Dispatch-Queue (optional) und führt einen finalen Batch-Flush aller Sinks aus.
    // - Schließt/entsorgt Sinks (IAsyncDisposable.DisposeAsync).
    Task StopAsync(CancellationToken ct);
}
