// Skynet.Core.Bootstrap/App.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

public static class App
{
    public static async Task<int> Run(CancellationToken ct = default)
    {
        ServiceProvider? sp = null;
        try
        {
            // Bootstrapper + Pipeline
            var bootstrapper = new Bootstrapper();
            var steps = BootstrapPipeline.CreateInitialPipeline(bootstrapDir: null);

            sp = (ServiceProvider)await bootstrapper.RunAsync(steps, ct).ConfigureAwait(false);

            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("App");
            logger.LogInformation("Application initialized. Level={Level}", bootstrapper.CurrentLevel);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 1223;
        }
        catch (Exception ex)
        {
            try
            {
                Console.Error.WriteLine($"[FATAL] {DateTime.UtcNow:O} App bootstrap failed: {ex}");
                var dir = "./bootstrap";
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "error.log");
                File.AppendAllText(logPath, $"[FATAL] {DateTime.UtcNow:O} {ex}{Environment.NewLine}");
            }
            catch { }
            return -1;
        }
        finally
        {
            try
            {
                if (sp is not null)
                {

                    sp.GetService<BootstrapFileLoggerProvider>()?.Flush();
                    await sp.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // Shutdown-Fehler ignorieren
            }
        }
    }
}
