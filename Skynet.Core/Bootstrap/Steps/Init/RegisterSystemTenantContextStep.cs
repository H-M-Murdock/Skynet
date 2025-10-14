// Skynet.Core.Bootstrap/Steps/RegisterSystemTenantContextStep.cs

using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Tenant;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert den festen SystemTenantContext (ITenantContext) für die Core-Phase.
/// </summary>
public sealed class RegisterSystemTenantContextStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Init;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = string.Empty;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        services.AddSingleton<ITenantContext, SystemTenantContext>();
        _report = $"system tenant context registered: {SystemTenantContext.SystemGuid}";
        return Task.CompletedTask;
    }

    public string GetReport() => _report;
}
