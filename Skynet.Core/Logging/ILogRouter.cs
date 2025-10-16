namespace Skynet.Core.Logging;

// Router wählt den passenden Sink je Event (z. B. nach Tenant/Component/Level/Datum).
// Ziel: Entkopplung von Routing-Entscheidung und konkretem IO (Datei/Console/etc.).
public interface ILogRouter
{
    // Liefert einen Sink, der das angegebene Event schreiben soll.
    // Implementierung kann Caching/Pooling von Sinks enthalten.
    ILogSink Resolve(ILogEvent evt);
}
