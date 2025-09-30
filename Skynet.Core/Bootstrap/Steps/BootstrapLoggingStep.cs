// Skynet.Core.Bootstrap/Steps/BootstrapLoggingStep.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap.Steps;

public sealed class BootstrapLoggingStep : IBootStep
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private readonly string _bootstrapDir;

    public BootstrapLoggingStep(string? bootstrapDirectory = null)
    {
        // Standard: lokales Bootstrap-Verzeichnis
        _bootstrapDir = string.IsNullOrWhiteSpace(bootstrapDirectory) ? "./bootstrap" : bootstrapDirectory;
    }

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // 1) Minimal: Console-Logging
        services.AddLogging(b => b.ClearProviders().AddSimpleConsole());

        // 2) Bootstrap-Verzeichnis anlegen und Schreibprobe
        var state = new BootstrapLoggingState();
        try
        {
            Directory.CreateDirectory(_bootstrapDir);

            var probeFile = Path.Combine(_bootstrapDir, ".write_probe");
            File.WriteAllText(probeFile, $"probe:{DateTime.UtcNow:O}");
            File.Delete(probeFile);

            state.CanWriteFiles = true;
            state.LogDirectory = _bootstrapDir;
        }
        catch
        {
            state.CanWriteFiles = false;
            state.LogDirectory = null;
        }

        services.AddSingleton(state);
        return Task.CompletedTask;
    }
}
