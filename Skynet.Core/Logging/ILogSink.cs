namespace Skynet.Core.Logging;

// Endziel der Log-Events (z. B. Datei, Console, Forwarder).
// Sollte intern effizient pufferieren und Flush unterstützen.
public interface ILogSink
{
    // Schreibt ein einzelnes Event. Darf intern pufferieren.
    Task WriteAsync(ILogEvent evt, CancellationToken ct);

    // Erzwingt einen Flush (z. B. bei Shutdown oder Zeitintervallen).
    Task FlushAsync(CancellationToken ct);
}
