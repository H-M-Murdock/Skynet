// Skynet.Core/Logging/CoreFileLoggerOptions.cs

using Microsoft.Extensions.Configuration;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapFileLoggerOptions
{
    // Basiseinstellungen
    public string DirectoryPath { get; set; } = "./logs";
    public string FileNamePrefix { get; set; } = "app";

    // Größen/Rotation
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetentionDays { get; set; } = 7;

    // Flush-Strategie
    public int FlushIntervalMs { get; set; } = 1000; // weniger aggressiv
    public bool UseWriteThrough { get; set; } = false; // standardmäßig aus

    // Sonstiges
    public bool UseUtcTimestamps { get; set; } = true;
    public bool Enabled { get; set; } = true;

    internal long MaxFileSizeBytes => (long)MaxFileSizeMb * 1024 * 1024;
    internal TimeSpan FlushInterval => TimeSpan.FromMilliseconds(FlushIntervalMs);

    // Konfigurationsbindung + Validierung
    public static BootstrapFileLoggerOptions FromConfiguration(IConfiguration? cfg, string sectionPath = "Logging:BootstrapFile")
    {
        var opts = new BootstrapFileLoggerOptions();
        if (cfg is not null)
        {
            var sec = cfg.GetSection(sectionPath);
            if (sec.Exists())
                sec.Bind(opts);
        }

        // Validation (sanity)
        if (string.IsNullOrWhiteSpace(opts.DirectoryPath))
            opts.DirectoryPath = "./logs";
        if (string.IsNullOrWhiteSpace(opts.FileNamePrefix))
            opts.FileNamePrefix = "app";

        if (opts.MaxFileSizeMb < 1) opts.MaxFileSizeMb = 1;
        if (opts.MaxFileSizeMb > 1024) opts.MaxFileSizeMb = 1024; // 1 GiB max

        if (opts.RetentionDays < 0) opts.RetentionDays = 0;
        if (opts.RetentionDays > 3650) opts.RetentionDays = 3650;

        if (opts.FlushIntervalMs < 50) opts.FlushIntervalMs = 50;
        if (opts.FlushIntervalMs > 60_000) opts.FlushIntervalMs = 60_000;

        return opts;
    }
}
