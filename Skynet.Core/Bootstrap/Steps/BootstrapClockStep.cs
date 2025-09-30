// Skynet.Core.Bootstrap/Steps/BootstrapClockStep.cs
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Time;

namespace Skynet.Core.Bootstrap.Steps;

public sealed class BootstrapClockStep : IBootStep
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // Produktionsuhr bereitstellen
        services.AddSingleton<IClock, SystemClock>();
        return Task.CompletedTask;
    }
}
