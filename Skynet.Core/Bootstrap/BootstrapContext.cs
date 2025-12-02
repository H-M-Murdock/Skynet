using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Logging;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapContext : IAsyncDisposable
{
    public IServiceCollection Services { get; } = new ServiceCollection();
    public Dictionary<string, object> Items { get; } = new();
    
    public ILoggerFactory LoggerFactory { get; private set; }
    public ILogger Logger { get; private set; }
    
    // Wir merken uns die Sink, um sie am Ende sauber zu disposen
    private ILogSink? _currentSink;

    public BootstrapContext()
    {
        // Start: Console
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddSimpleConsole());
        Logger = LoggerFactory.CreateLogger("Bootstrap");
    }

    /// <summary>
    /// Schaltet vom reinen Console-Logging auf File-Logging um.
    /// Pfad wird aus context.Items["Path:Root"] gelesen (gesetzt durch InitFilesystemStep).
    /// </summary>
    public void UseFileLogging()
    {
        if (!Items.TryGetValue("Path:Root", out var rootObj) || rootObj is not string rootPath)
        {
            Logger.LogWarning("Cannot switch to FileLogging: 'Path:Root' not found in context.");
            return;
        }

        var logPath = Path.Combine(rootPath, "bootstrap.log");
        
        // Formatter und Sink manuell erstellen (kein DI vorhanden)
        var formatter = new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: true);
        var fileSink = new SimpleFileSink(logPath, formatter);
        
        // Sink merken für Dispose
        _currentSink = fileSink;

        // Upgrade durchführen
        UpgradeLogging(builder =>
        {
            // Wir fügen unseren eigenen Provider hinzu
            builder.AddProvider(new BootstrapLoggerProvider(fileSink));
        });
        
        Logger.LogInformation($"Switched logging to file: {logPath}");
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
        
        // Sink disposen (FileStream schließen)
        if (_currentSink != null)
        {
            await _currentSink.DisposeAsync();
        }

        await ValueTask.CompletedTask;
    }
}
