namespace Skynet.Core.Logging;

// Repräsentiert eine einzelne Client-Verbindung.
// Liefert bereits geframte Nutzlasten (z. B. Length-Prefix, eine Payload = ein Event).
public interface IEventChannel : IAsyncDisposable
{
    // Liest den nächsten Frame. Null signalisiert Ende der Verbindung.
    Task<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken ct);

    // Schliesst den Kanal.
    Task CloseAsync(CancellationToken ct);
}
