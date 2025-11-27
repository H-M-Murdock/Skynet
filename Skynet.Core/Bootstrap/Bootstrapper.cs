using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

public sealed class Bootstrapper
{
    private readonly List<IBootStep> _steps = new();

    public Bootstrapper AddStep(IBootStep step)
    {
        _steps.Add(step);
        return this;
    }

    public async Task<BootstrapResult> RunAsync()
    {
        BootstrapContext? context = null;
        try
        {
            context = new BootstrapContext();
            context.Logger.LogInformation("=== Bootstrapper Started ===");

            foreach (var step in _steps)
            {
                using (context.Logger.BeginScope("Component: {ComponentName}", step.Name))
                {
                    context.Logger.LogInformation("[Begin] Initializing component");

                    string report;
                    try
                    {
                        report = await step.ExecuteAsync(context);
                    }
                    catch (Exception stepEx)
                    {
                        context.Logger.LogCritical(stepEx, "[FATAL] Initialization failed.");
                        return BootstrapResult.Failure(stepEx, BootstrapExitCode.GeneralError, context);
                    }

                    // Logge Complete MIT dem Bericht aus dem Step
                    context.Logger.LogInformation("[Complete] {Report}", report);
                }
            }
            
            // Wir übergeben die Factory (die wir mühsam aufgebaut haben) an den Container
            context.Services.AddSingleton<ILoggerFactory>(context.LoggerFactory);
            
            // Wir ermöglichen ILogger<T> Injection in der fertigen App
            context.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            context.Logger.LogInformation("=== Bootstrapper Completed ===");

            return BootstrapResult.Success(context.Services.BuildServiceProvider(), context);
        }
        catch (Exception globalEx)
        {
            return BootstrapResult.Failure(globalEx, BootstrapExitCode.GeneralError, context);
        }
    }
}
