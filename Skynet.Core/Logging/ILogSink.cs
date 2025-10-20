namespace Skynet.Core.Logging;

// Endziel der Log-Events (z. B. Datei, Console, Forwarder).
// Sollte intern effizient pufferieren und Flush unterstützen.
public interface ILogSink : IAsyncDisposable
{
    // Schreibt ein einzelnes Event. Darf intern pufferieren.
    Task WriteAsync(ILogEvent evt, CancellationToken ct);

    // Erzwingt einen Flush (z. B. bei Shutdown oder Zeitintervallen).
    Task FlushAsync(CancellationToken ct);

    // Optionaler Lifecycle-Hook: Ressourcen öffnen/initialisieren (idempotent).
    // Darf mehrfach aufgerufen werden; Implementierungen sorgen für Idempotenz.
    Task StartAsync(CancellationToken ct);
}
