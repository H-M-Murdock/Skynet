// Optionen: Konfiguration des Clients (Encoder/Transport/Backpressure) und Routing (by-tenant/by-component etc.)

public sealed class LoggingClientOptions
{
    public ILogEventEncoder Encoder { get; init; } = default!;
    public IEventTransportFactory TransportFactory { get; init; } = default!;
    public IBackpressurePolicy Backpressure { get; init; } = default!;

    public IEnricher? Enricher { get; init; }  // setzt EventId/CorrelationId/Tenant/Component etc.
    public IRedactionPolicy? Redaction { get; init; }
    public ISizeLimiter? SizeLimiter { get; init; }

    // Kapazität des In-Process-Puffers im Client. Bounded, um OOM zu vermeiden.
    public int QueueCapacity { get; init; } = 10_000;

    // Timeouts für Connect/Flush – nützlich für Bootstrap/Shutdown.
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan FlushTimeout { get; init; } = TimeSpan.FromSeconds(5);
}

public sealed class LoggingRoutingOptions
{
    // Steuerung der Pfadbildung: Partitionierung nach Tenant/Component.
    public bool ByTenant { get; init; } = true;
    public bool ByComponent { get; init; } = true;

    // Default-Template; Renderer setzt fehlende Platzhalter defensiv (z. B. "unknown").
    public string PathTemplate { get; init; } = "logs/{tenant}/{component}/{yyyy-MM-dd}.log";

    // Zeitbasierte Rotation (täglich) zusätzlich/anstelle von size-basiert.
    public bool DailyRotation { get; init; } = true;

    // Größe/Anzahl für size-basierte Rotation (optional).
    public long? MaxFileBytes { get; init; }
    public int? MaxFiles { get; init; }

    // Optional: Mindestlevel je Komponente (z. B. "Skynet.OrderService" -> "Warning").
    public IReadOnlyDictionary<string, string>? MinimumLevelPerComponent { get; init; }
}
