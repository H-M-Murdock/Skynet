// Skynet.Core.Bootstrap/Steps/RegisterSystemTenantResourcesStep.cs

using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Tenant;

namespace Skynet.Core.Bootstrap;

public sealed class RegisterSystemTenantResourcesStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = string.Empty;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        var sys = new SystemTenantResources();
        services.AddSingleton<ITenantResources>(sys);
        _report = $"system tenant resources registered at {SystemTenantResources.SystemTenantGuid}";
        return Task.CompletedTask;
    }

    public string GetReport() => _report;
}
