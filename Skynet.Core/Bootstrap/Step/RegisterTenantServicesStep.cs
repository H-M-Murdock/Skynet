using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Tenant;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert die Mandanten-Infrastruktur und aktiviert den globalen System-Kontext.
/// </summary>
public class RegisterTenantServicesStep : IBootStep
{
    public string Name => "Register Tenant Services";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // 1. Services für die Ziel-Anwendung registrieren
        // Der Accessor wird als Singleton registriert (stateful via AsyncLocal)
        context.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        
        // Den GlobalTenantContext ebenfalls registrieren, falls man explizit den System-Kontext injecten möchte
        context.Services.AddSingleton(GlobalTenantContext.Instance);

        // 2. Initialen Kontext setzen
        // Da TenantContextAccessor auf einem statischen AsyncLocal basiert, können wir hier 
        // eine Instanz erzeugen und den Wert setzen. Dies gilt dann für den laufenden 
        // Bootstrap-Thread und alle folgenden Async-Operationen.
        var accessor = new TenantContextAccessor();
        accessor.Current = GlobalTenantContext.Instance;

        return Task.FromResult("Tenant Services registered & Global Context activated.");
    }
}
