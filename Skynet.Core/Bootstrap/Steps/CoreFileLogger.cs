// Skynet.Core/Logging/CoreFileLogger.cs

using System.Text;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

internal interface ILogWriter
{
    void Post(string line);
}

internal sealed class CoreFileLogger : ILogger
{
    private readonly string _category;
    private readonly ILogWriter _writer;

    public CoreFileLogger(string category, ILogWriter writer)
    {
        _category = category;
        _writer = writer;
    }

    public IDisposable? BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var line = CoreLogFormatter.Format(_category, logLevel, state, exception, formatter);
        _writer.Post(line);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

internal static class CoreLogFormatter
{
    public static string Format<TState>(string category, LogLevel level, TState state, Exception? ex,
        Func<TState, Exception?, string> formatter)
    {
        var now = DateTime.UtcNow;
        var msg = SafeFormat(formatter, state, ex);

        var sb = new StringBuilder(256);
        sb.Append(now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        sb.Append(" [");
        sb.Append(level.ToString());
        sb.Append("] ");
        sb.Append(category);
        sb.Append(": ");
        sb.Append(msg);

        if (ex != null)
        {
            sb.AppendLine();
            sb.Append(ex);
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string SafeFormat<TState>(Func<TState, Exception?, string> formatter, TState state, Exception? ex)
    {
        try { return formatter(state, ex); }
        catch { return state?.ToString() ?? string.Empty; }
    }
}
