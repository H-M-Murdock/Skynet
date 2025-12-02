using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Skynet.Core.i18n;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert die i18n-Infrastruktur (Repository, Lookup, Renderer, Service).
/// </summary>
public sealed class RegisterI18NServicesStep : IBootStep
{
    public string Name => "Register i18n Services";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // 1. Basis-Komponenten
        context.Services.TryAddSingleton<ITemplateRepository, InMemoryTemplateRepository>();
        context.Services.TryAddSingleton<ITemplateRenderer, SimpleTemplateRenderer>();
        context.Services.TryAddSingleton<ITenantScopeFormatter, DefaultTenantScopeFormatter>();

        // 2. Logik-Services
        context.Services.TryAddSingleton<ITemplateLookupService, DefaultTemplateLookupService>();
        context.Services.TryAddSingleton<ILocalizationContextAccessor, DefaultLocalizationContextAccessor>();
        
        // 3. High-Level API
        context.Services.TryAddSingleton<ILocalizationService, DefaultLocalizationService>();

        return Task.FromResult("i18n services registered (InMemory defaults)");
    }
}
