
namespace Skynet.Core.Bootstrap;

/// <summary>
/// Definiert den Standard-Startvorgang des Frameworks.
/// </summary>
internal static class SkynetDefaultPipeline
{
    public static void ConfigureDefaults(Bootstrapper bootstrapper)
    {
        // 1. Logging zuerst (System-Anforderung)
        //bootstrapper.AddStep(new InitLoggingStep());
        
        // 2. Tenant Context (Architektur-Anforderung)
        //bootstrapper.AddStep(new SystemTenantStep());
        
        // 3. Core Services (Lizenz, Crypto, etc.)
        //bootstrapper.AddStep(new RegisterCoreServicesStep());
        
        // Hier können zukünftig weitere Standard-Steps hinzukommen,
        // ohne dass die Consuming App geändert werden muss.
    }
}
