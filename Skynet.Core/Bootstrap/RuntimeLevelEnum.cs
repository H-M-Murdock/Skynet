// C#
namespace Skynet.Core.Bootstrap;

public enum RuntimeLevel
{
    Bootstrap = 0,  // nur rudimentäre Dienste (Console-Logging, einfache Config)
    Init      = 1,
    Core      = 2,   // zentrale Abstraktionen initialisiert (TenantContext, Locator, Options)
    Infra     = 3,   // Infrastruktur online (Storage, Netzwerk, Telemetrie)
    Running   = 4    // Betriebsmodus (Caches warm, HealthChecks grün)
}
