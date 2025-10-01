// Skynet.Core.Bootstrap/Steps/RegisterAllResourceProvidersStep.cs
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.ResourceProvider;

namespace Skynet.Core.Bootstrap;

public sealed class RegisterAllResourceProvidersStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Init;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = string.Empty;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // Standard-Roots und Prioritäten direkt hier definieren
        var liveRoot = "./root";
        var bootstrapRoot = "./bootstrap";
        var tempRoot = Path.Combine(Path.GetTempPath(), "Skynet");

        var livePriority = 90;
        var bootstrapPriority = 95;
        var tempPriority = 98;

        services.AddSingleton<IResourceProvider>(new FileSystemResourceProvider(liveRoot, livePriority));
        services.AddSingleton<IResourceProvider>(new FileSystemResourceProvider(bootstrapRoot, bootstrapPriority));
        services.AddSingleton<IResourceProvider>(new FileSystemResourceProvider(tempRoot, tempPriority));

        _report =
            $"Providers registered: live='{Path.GetFullPath(liveRoot)}'(prio={livePriority}), " +
            $"bootstrap='{Path.GetFullPath(bootstrapRoot)}'(prio={bootstrapPriority}), " +
            $"temp='{Path.GetFullPath(tempRoot)}'(prio={tempPriority})";

        return Task.CompletedTask;
    }

    public string GetReport() => _report;
}
