namespace Skynet.Core.Logging;

// Server-seitiger Listener (UDS/Pipe/TCP), der eingehende Verbindungen akzeptiert.
public interface IEventListener
{
    // Bindet an Endpunkt und macht für Accept bereit.
    Task BindAsync(CancellationToken ct);

    // Akzeptiert eine Verbindung und gibt einen lesbaren Kanal zurück.
    Task<IEventChannel> AcceptAsync(CancellationToken ct);

    // Beendet das Annehmen neuer Verbindungen.
    Task CloseAsync(CancellationToken ct);
}
