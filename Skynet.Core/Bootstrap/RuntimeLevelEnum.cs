// C#
namespace Skynet.Core.Bootstrap;

public enum RuntimeLevel
{
    Bootstrap = 0,  // nur rudimentäre Dienste (Console-Logging, einfache Config)
    Core      = 1,   // zentrale Abstraktionen initialisiert (TenantContext, Locator, Options)
    Infra     = 2,   // Infrastruktur online (Storage, Netzwerk, Telemetrie)
    Running   = 3    // Betriebsmodus (Caches warm, HealthChecks grün)
}
