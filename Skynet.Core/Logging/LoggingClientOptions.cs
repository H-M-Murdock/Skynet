namespace Skynet.Core.Logging;

public sealed class LoggingClientOptions
{
    public int QueueCapacity { get; set; } = 1024;
    public int BatchSize { get; set; } = 128;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    // Statt ClientBackpressureMode:
    public DropMode DropMode { get; set; } = DropMode.DropNewest;

    // Optional: Policy für dynamische Entscheidung
    public IBackpressurePolicy? BackpressurePolicy { get; set; }
}