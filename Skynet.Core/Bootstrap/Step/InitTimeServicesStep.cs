using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Time;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert die grundlegenden Zeit-Dienste (Uhr, Stopwatch).
/// Diese Dienste haben keine Abhängigkeiten und werden sehr früh benötigt.
/// </summary>
public class InitTimeServicesStep : IBootStep
{
    public string Name => "Initialize Time Services";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // 1. Systemuhr (Echtzeit)
        // Singleton ist hier korrekt, da SystemClock zustandslos ist.
        context.Services.AddSingleton<IClock, SystemClock>();

        // 2. Monotone Uhr (Messungen)
        // Auch zustandslos -> Singleton.
        // WICHTIG: Wir registrieren deine eigene Klasse Skynet.Core.Time.Stopwatch, 
        // nicht System.Diagnostics.Stopwatch direkt.
        context.Services.AddSingleton<IStopwatch, Skynet.Core.Time.Stopwatch>();

        return Task.FromResult("Registered: IClock (SystemClock), IStopwatch (Monotonic)");
    }
}
