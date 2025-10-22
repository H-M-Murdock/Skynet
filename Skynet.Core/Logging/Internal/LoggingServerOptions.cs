using System.Threading.Channels;

namespace Skynet.Core.Logging;

/// <summary>
/// Optionen für den Logging-Server.
/// </summary>
public sealed class LoggingServerOptions
{
    /// <summary>
    /// Maximale Länge der zentralen Dispatch-Queue. Schützt vor OOM.
    /// </summary>
    public int MaxQueueLength { get; init; } = 10_000;

    /// <summary>
    /// Anzahl der parallelen Dispatch-Worker.
    /// </summary>
    public int WorkerCount { get; init; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>
    /// Verhalten, wenn die Queue voll ist: Wait (blockiert Producer) oder DropNewest/DropOldest/DropWrite.
    /// </summary>
    public BoundedChannelFullMode QueueFullMode { get; init; } = BoundedChannelFullMode.DropWrite;

    /// <summary>
    /// Rückoff zwischen fehlgeschlagenen Accept-Versuchen (z. B. InMemory-Listener).
    /// </summary>
    public TimeSpan AcceptBackoff { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Intervall für periodisches Flush aller Sinks. TimeSpan.Zero deaktiviert den Timer.
    /// </summary>
    public TimeSpan PeriodicFlushInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximale Zeit für Stop/Drain, bevor hart abgebrochen wird.
    /// </summary>
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(10);
}