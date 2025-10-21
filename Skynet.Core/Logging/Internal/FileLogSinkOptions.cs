namespace Skynet.Core.Logging;

/// <summary>
/// Optionen für FileLogSink (Streaming).
/// </summary>
public sealed class FileLogSinkOptions
{
    // Basis-Root für Logs (absoluter Pfad empfohlen)
    public string? BaseRoot { get; set; }

    // Tenant-Ordner (Segment unterhalb von BaseRoot)
    public string? Tenant { get; set; } = "logs";

    // Optionaler Subfolder unterhalb des Tenants
    public string? SubFolder { get; set; }

    // Template, das der PathTemplateRenderer versteht (key im IoUtilities-Sinn)
    public string PathTemplate { get; set; } = "{yyyy-MM-dd}/{level}.log";

    // Zeitquelle für Path (true = Event.Timestamp, false = Jetzt)
    public bool UseEventTimestampForPath { get; set; } = true;

    // Puffergröße, ab der sofort geschrieben wird (0 = nur zeitgesteuert/Flush/Dispose)
    public int BufferFlushBytes { get; set; } = 64 * 1024;

    // Flush-Intervall in Millisekunden (0 = kein Timer)
    public int FlushIntervalMs { get; set; } = 1000;

    // Write-Through (Flush nach jedem Write)
    public bool WriteThrough { get; set; } = false;

    // Maximalgröße pro Datei (Bytes); 0 = keine Rotation
    public long MaxFileBytes { get; set; } = 50L * 1024 * 1024; // 50 MiB Default

    // Retention: Dateien löschen, die älter als N Tage sind (0 = aus)
    public int RetentionDays { get; set; } = 7;

    // Optional: Obergrenze für Gesamtgröße (Bytes) im Tenant/Subfolder-Baum (0 = aus)
    public long MaxDirectoryBytesApprox { get; set; } = 0;

    // Intervall für Retention-Check (ms, 0 = aus)
    public int RetentionCheckIntervalMs { get; set; } = 60_000;
}