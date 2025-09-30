// Skynet.Core.Bootstrap/App.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skynet.Core.Bootstrap.Steps;

namespace Skynet.Core.Bootstrap;

public static class App
{
    public static async Task<int> Run(CancellationToken ct = default)
    {
        try
        {
            // Bootstrapper + Pipeline
            var bootstrapper = new Bootstrapper();
            var steps = BootstrapPipeline.CreateInitialPipeline(bootstrapDir: null);

            var sp = await bootstrapper.RunAsync(steps, ct).ConfigureAwait(false);

            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("App");
            logger.LogInformation("Application initialized. Level={Level}", bootstrapper.CurrentLevel);

            // TODO: hier später weitere Pipelines/Start-Tasks anhängen (Infra/Running)

            return 0;
        }
        catch (OperationCanceledException)
        {
            // Sauberer Abbruch
            return 1223; // gängiger Code für canceled
        }
        catch (Exception ex)
        {
            // Fallback-Fehlerausgabe: erst Console, dann versuchen wir auf Bootstrap-Verzeichnis zu schreiben
            try
            {
                Console.Error.WriteLine($"[FATAL] {DateTime.UtcNow:O} App bootstrap failed: {ex}");

                // einfacher Notfallversuch: in ./bootstrap/error.log
                var dir = "./bootstrap";
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "error.log");
                File.AppendAllText(logPath, $"[FATAL] {DateTime.UtcNow:O} {ex}{Environment.NewLine}");
            }
            catch
            {
                // Ignorieren: wir sind bereits im fatalen Pfad
            }
            return -1;
        }
    }
}
