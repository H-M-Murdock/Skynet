namespace Skynet.Core.Logging;

/// <summary>
/// Optionen für SizeLimiter.
/// </summary>
public sealed class SizeLimitOptions
{
    public int MaxStringLength { get; set; } = 0;        // 0 = deaktiviert
    public int MaxStateEntries { get; set; } = 0;        // 0 = deaktiviert
    public int MaxTotalBytesApprox { get; set; } = 0;    // 0 = deaktiviert
    public string Ellipsis { get; set; } = "…";
}