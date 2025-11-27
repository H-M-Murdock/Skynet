using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapContext : IAsyncDisposable
{
    public IServiceCollection Services { get; } = new ServiceCollection();
    public Dictionary<string, object> Items { get; } = new();
    
    public ILoggerFactory LoggerFactory { get; private set; }
    public ILogger Logger { get; private set; }

    public BootstrapContext()
    {
        // Start: Console
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddSimpleConsole());
        Logger = LoggerFactory.CreateLogger("Bootstrap");
    }

    public void UpgradeLogging(Action<ILoggingBuilder> configure)
    {
        // Alten Logger flushen/disposen
        LoggerFactory.Dispose();

        // Neuen bauen
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => 
        {
            b.AddSimpleConsole(); // Konsole behalten wir meistens bei
            configure(b);
        });
        Logger = LoggerFactory.CreateLogger("Bootstrap");
    }

    public async ValueTask DisposeAsync()
    {
        // Sicherstellen, dass alle Logs geschrieben werden
        if (LoggerFactory is IDisposable d) d.Dispose();
        await ValueTask.CompletedTask;
    }
}
