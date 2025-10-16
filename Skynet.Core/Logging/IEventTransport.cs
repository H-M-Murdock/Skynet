namespace Skynet.Core.Logging;

public interface IEventTransport
{
    // Aktueller Zustand – für Telemetrie/Diagnose.
    TransportState State { get; }

    // Aufbau einer Verbindung zum lokalen Logging-Server/Worker. Sollte wiederverbindungsfähig sein.
    Task ConnectAsync(CancellationToken ct);

    // Versucht die Nutzlast (ein Event-Frame) zu senden. Non-blocking bevorzugt:
    // - true: akzeptiert (ggf. gepuffert)
    // - false: abgewiesen (Backpressure/Fehler); Caller darf droppen oder retryen
    ValueTask<bool> TrySendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);

    // Leitet einen Flush ein (sendet Puffer, wartet auf Bestätigung falls möglich).
    Task FlushAsync(CancellationToken ct);

    // Schließt die Verbindung kontrolliert (z. B. am App-Ende).
    Task CloseAsync(CancellationToken ct);
}