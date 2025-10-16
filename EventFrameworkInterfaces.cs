// Server/Worker: Nimmt Event-Frames an, decodiert und leitet an Router/Sink weiter.
// Listener/Channel abstrahieren die Gegenstelle zum Transport (Accept/Read-Loop).

public interface IEventListener
{
    // Bindet Server-Endpunkt (UDS/Pipe/TCP) und macht ihn bereit für Accepts.
    Task BindAsync(CancellationToken ct);

    // Akzeptiert eine neue Verbindung und liefert einen Kanal für eingehende Frames.
    Task<IEventChannel> AcceptAsync(CancellationToken ct);

    // Schließt den Listener (keine neuen Verbindungen).
    Task CloseAsync(CancellationToken ct);
}

// Ein Channel repräsentiert eine Client-Verbindung. ReadAsync liefert bereits geframte Payloads.
// Framing (z. B. Length-Prefix) ist Aufgabe von Transport/Channel-Implementierung.
public interface IEventChannel : IAsyncDisposable
{
    // Nächster Frame; null bzw. leeres Ergebnis signalisiert Ende der Verbindung.
    Task<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken ct);

    // Schließt den Kanal.
    Task CloseAsync(CancellationToken ct);
}

public interface ILoggingServer
{
    // Startet den Accept-/Read-/Dispatch-Loop (typisch: Hintergrund-Task).
    Task StartAsync(CancellationToken ct);

    // Stoppt sauber (existierende Kanäle schließen, Flush der Sinks).
    Task StopAsync(CancellationToken ct);
}
