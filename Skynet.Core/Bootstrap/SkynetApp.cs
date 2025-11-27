using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

public static class SkynetApp
{
    /// <summary>
    /// Startet die Skynet-Anwendung mit der Standard-Pipeline.
    /// </summary>
    /// <param name="onBootSuccess">Wird nach erfolgreichem Start ausgeführt.</param>
    /// <param name="configureCustomSteps">Optional: Zum Hinzufügen eigener Steps ZUSÄTZLICH zum Standard.</param>
    /// <returns>Exit Code</returns>
    public static async Task<int> RunAsync(
        Func<IServiceProvider, Task> onBootSuccess,
        Action<Bootstrapper>? configureCustomSteps = null)
    {
        var bootstrapper = new Bootstrapper();
        
        // 1. Wir laden IMMER die Framework-Defaults.
        // Die App muss nicht wissen, was dazugehört.
        SkynetDefaultPipeline.ConfigureDefaults(bootstrapper);

        // 2. Falls die spezifische App noch eigene Boot-Steps hat (z.B. DB-Migration),
        // können diese hier angehängt werden.
        configureCustomSteps?.Invoke(bootstrapper);

        await using var result = await bootstrapper.RunAsync();

        if (!result.IsSuccess)
        {
            return (int)result.ExitCode;
        }

        try
        {
            await onBootSuccess(result.ServiceProvider!);
            return 0;
        }
        catch (Exception ex)
        {
            // Versuch, den Logger aus dem Container zu holen
            var logger = result.ServiceProvider?.GetService<ILoggerFactory>()?.CreateLogger("SkynetApp");
        
            if (logger != null)
            {
                logger.LogCritical(ex, "Application crashed during runtime execution.");
            }
            else
            {
                // --- FIX: Fallback, falls DI kaputt ist ---
                Console.Error.WriteLine("\n!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.Error.WriteLine("[FATAL RUNTIME CRASH] Logger not available!");
                Console.Error.WriteLine(ex.ToString());
                Console.Error.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\n");
            }

            return -1; 
        }
    }
}
