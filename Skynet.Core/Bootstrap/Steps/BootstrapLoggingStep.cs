// Skynet.Core.Bootstrap/Steps/BootstrapLoggingStep.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapLoggingStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private readonly string _bootstrapDir;
    private bool _canWrite;
    private string? _path;

    public BootstrapLoggingStep(string? bootstrapDirectory = null)
    {
        _bootstrapDir = string.IsNullOrWhiteSpace(bootstrapDirectory) ? "./bootstrap" : bootstrapDirectory;
    }

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        services.AddLogging(b => b.ClearProviders().AddSimpleConsole());

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

        _canWrite = state.CanWriteFiles;
        _path = state.LogDirectory;

        services.AddSingleton(state);
        return Task.CompletedTask;
    }

    public string GetReport()
        => $"bootstrap directory writable={_canWrite}, path={_path ?? "<none>"}";
}
