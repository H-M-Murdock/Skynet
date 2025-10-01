// Skynet.Core/Logging/CoreFileLoggerOptions.cs

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapFileLoggerOptions
{
    public string DirectoryPath { get; set; } = "./logs";
    public string FileNamePrefix { get; set; } = "app";
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetentionDays { get; set; } = 7;
    public int FlushIntervalMs { get; set; } = 500;
    public bool UseUtcTimestamps { get; set; } = true;
    public bool Enabled { get; set; } = true;

    internal long MaxFileSizeBytes => (long)MaxFileSizeMb * 1024 * 1024;
    internal TimeSpan FlushInterval => TimeSpan.FromMilliseconds(FlushIntervalMs);
}
