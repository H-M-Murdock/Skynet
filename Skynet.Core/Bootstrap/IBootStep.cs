// C#

using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

public interface IBootStep
{
    // Ab welchem Level darf dieser Schritt ausgeführt werden (z. B. Bootstrap)?
    RuntimeLevel MinLevel { get; }

    // Welchen Ziel-Level erreicht man nach erfolgreichem Schritt (z. B. Core)?
    RuntimeLevel TargetLevel { get; }

    // Führt die Registrierung/Initialisierung aus (kann Services hinzufügen oder starten).
    Task ExecuteAsync(IServiceCollection services, CancellationToken ct);
}
