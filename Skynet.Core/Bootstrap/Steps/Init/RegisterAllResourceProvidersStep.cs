// Skynet.Core.Bootstrap/Steps/RegisterAllResourceProvidersStep.cs
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.ResourceProvider;

namespace Skynet.Core.Bootstrap;

public sealed class RegisterAllResourceProvidersStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Init;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = string.Empty;

    public RegisterAllResourceProvidersStep() { }

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        var liveRoot = "./root";
        var bootstrapRoot = "./bootstrap";
        var tempRoot = Path.Combine(Path.GetTempPath(), "Skynet");

        var livePriority = 90;
        var bootstrapPriority = 95;
        var tempPriority = 98;

        // Filesystem-Provider
        services.AddSingleton<IResourceReader>(new FileSystemResourceReader(liveRoot, livePriority));
        services.AddSingleton<IResourceReader>(new FileSystemResourceReader(bootstrapRoot, bootstrapPriority));
        services.AddSingleton<IResourceReader>(new FileSystemResourceReader(tempRoot, tempPriority));

        // Environment-Provider (Prefix konfigurierbar, hier "SKYNET")
        services.AddSingleton<IResourceReader>(new EnvironmentResourceReader(EnvScope.Process, 30, "SKYNET"));
        services.AddSingleton<IResourceReader>(new EnvironmentResourceReader(EnvScope.User,    35, "SKYNET"));
        services.AddSingleton<IResourceReader>(new EnvironmentResourceReader(EnvScope.Machine, 40, "SKYNET"));

        _report =
            "Providers registered:" + Environment.NewLine +
            $"  FS:   live='{Path.GetFullPath(liveRoot)}' (prio={livePriority})" + Environment.NewLine +
            $"        bootstrap='{Path.GetFullPath(bootstrapRoot)}' (prio={bootstrapPriority})" + Environment.NewLine +
            $"        temp='{Path.GetFullPath(tempRoot)}' (prio={tempPriority})" + Environment.NewLine +
            $"  ENV:  process (prio=30), user (prio=35), machine (prio=40)";

        return Task.CompletedTask;
    }

    public string GetReport() => _report;
}
