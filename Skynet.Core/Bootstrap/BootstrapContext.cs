using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Logging;
using Skynet.Core.Time;

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

        try 
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
            var logFileName  = Path.Combine(rootPath, $"bootstrap.{timestamp}.log");

            var retention = new BasicRetentionPolicy(maxFiles: 5);

            var formatter = new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: true);
            
            // Der Sink schreibt in targetFile und räumt im rootPath auf, basierend auf dem Pattern
            var fileSink = new SimpleFileSink(
                targetFilePath: logFileName,
                retentionRootPath: rootPath,
                retentionSearchPattern: "bootstrap*.log",
                formatter: formatter,
                retentionPolicy: retention
            );
            
            // 5. Start (triggert Retention)
            fileSink.StartAsync(CancellationToken.None).Wait();
            _currentSink = fileSink;

            UpgradeLogging(builder =>
            {
                builder.AddProvider(new BootstrapLoggerProvider(fileSink));
            });

            Logger.LogInformation($"Switched logging to file: {logFileName}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize file logging.");
        }
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
