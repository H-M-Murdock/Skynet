// Skynet.Core.Bootstrap/Steps/BootstrapLoggingStep.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapLoggingStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private readonly string _logDir;
    private bool _fileEnabled;
    private string? _path;

    public BootstrapLoggingStep(string? bootstrapDirectory = null)
    {
        // Default: ./bootstrap/log
        _logDir = string.IsNullOrWhiteSpace(bootstrapDirectory) ? "./bootstrap/log" : bootstrapDirectory;
    }

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // 1) Minimal: Console-Logger aktivieren (Fallback für frühe Ausgaben)
        services.AddLogging(b => b.ClearProviders().AddSimpleConsole());

        // 2) Verzeichnis anlegen und Probe schreiben
        var state = new BootstrapLoggingState();
        try
        {
            Directory.CreateDirectory(_logDir);

            var probeFile = Path.Combine(_logDir, ".write_probe");
            File.WriteAllText(probeFile, $"probe:{DateTime.UtcNow:O}");
            File.Delete(probeFile);

            state.CanWriteFiles = true;
            state.LogDirectory = _logDir;
        }
        catch (Exception ex)
        {
            state.CanWriteFiles = false;
            state.LogDirectory = null;

            throw new InvalidOperationException(
                $"BootstrapLoggingStep: Log-Verzeichnis nicht nutzbar ('{_logDir}'). Beende Start.", ex);
        }

        _fileEnabled = state.CanWriteFiles;
        _path = state.LogDirectory;

        services.AddSingleton(state);

        var opts = new BootstrapFileLoggerOptions
        {
            DirectoryPath = _logDir,
            FileNamePrefix = "core",
            Enabled = true,
            MaxFileSizeMb = 10,
            RetentionDays = 7,
            FlushIntervalMs = 500,
            UseUtcTimestamps = true
        };
        services.AddSingleton(opts);

        // Eine konkrete Provider-Instanz erstellen und im DI registrieren
        var fileProvider = new BootstrapFileLoggerProvider(opts);
        services.AddSingleton(fileProvider);

        // Logging auf die registrierte Instanz umstellen
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddProvider(fileProvider);
        });

        return Task.CompletedTask;
    }

    public string GetReport()
        => _fileEnabled
            ? $"logging: file-enabled, path={_path}"
            : "logging: file-disabled";
}
