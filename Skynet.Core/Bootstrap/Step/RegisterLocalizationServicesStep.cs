using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Skynet.Core.Localization;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert die Basis-Lokalisierungsdienste (Culture Store, Provider, Formatter).
/// </summary>
public sealed class RegisterLocalizationServicesStep : IBootStep
{
    public string Name => "Register Localization Services";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // Optionen definieren (könnten später auch aus Konfigurationsdateien kommen)
        var options = new LocalizationOptions
        {
            DefaultCulture = "en-US",
            SupportedCultures = new[] { "en-US", "de-DE", "de-CH" } 
        };

        // 1. Core Services registrieren (Store, Provider, Formatter, ScopeFactory)
        // Nutzt die existierende Extension-Method aus Skynet.Core.Localization
        context.Services.AddLocalizationCore(options);

        // 2. Ergänzende Services registrieren, die nicht in AddLocalizationCore enthalten sind
        context.Services.TryAddSingleton<ILocalizationChangeNotifier, DefaultLocalizationChangeNotifier>();

        return Task.FromResult($"Localization services registered. Default: {options.DefaultCulture}");
    }
}
