// Transport (Client-seitig): Verbindungsmanagement + asynchrones Senden.
// Implementierungen: Named Pipe (Windows), Unix Domain Socket (Linux), TCP-Loopback.

namespace Skynet.Core.Logging;

public enum TransportState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted,
    Closed
}