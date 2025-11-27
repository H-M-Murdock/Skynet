using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

/// <summary>
/// Leitet MEL-Logs an den ILoggingClient weiter.
/// </summary>
public sealed class LoggerProvider : ILoggerProvider
{
    private readonly ILoggingClient _client;
    private readonly ConcurrentDictionary<string, Logger> _loggers = new(StringComparer.Ordinal);

    public LoggerProvider(ILoggingClient client)
    {
        _client = client;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new Logger(name, _client));
    }

    public void Dispose()
    {
        _loggers.Clear();
        // Client wird nicht hier disposed, da er via DI als Singleton/HostedService verwaltet wird.
    }
}