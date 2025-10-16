namespace Skynet.Core.Logging;

// Leichte Health-/Status-Abfrage für Monitoring/Diagnostics.
public interface ILogHealth
{
    TransportState TransportState { get; }
    int QueueLength { get; }
    int QueueCapacity { get; }
    long DroppedCount { get; }
}
