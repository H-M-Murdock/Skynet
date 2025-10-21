using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

/// <summary>
/// Hilfsmethoden für stabile, kulturinvariante LogLevel-Strings.
/// </summary>
public static class LogLevelExtensions
{
    public static string ToInvariantString(this LogLevel level) => level switch
    {
        LogLevel.Trace => "Trace",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        LogLevel.None => "None",
        _ => "Information"
    };
}
