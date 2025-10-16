namespace Skynet.Core.Logging;

public interface ILoggingClient
{
    // Initialisiert Transport und interne Strukturen. Sollte idempotent sein.
    Task StartAsync(CancellationToken ct);

    // Nimmt ein Event entgegen:
    // - ruft Enricher/Redaction/SizeLimiter synchron auf
    // - encodiert Event
    // - legt es in eine bounded Queue oder sendet direkt (je nach Implementierung)
    // - wirft i. d. R. keine Exceptions (Best Effort); Droppen wird intern gezählt
    ValueTask LogAsync(ILogEvent evt, CancellationToken ct);

    // Spült Puffer soweit möglich.
    Task FlushAsync(CancellationToken ct);

    // Beendet: drain == true => Puffer leeren, dann stoppen; sonst sofort stoppen.
    Task StopAsync(bool drain, CancellationToken ct);

    // Metriken/Diagnose
    long DroppedCount { get; }
    int QueueLength { get; }
    int QueueCapacity { get; }
}