using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skynet.Core.Bootstrap;
using Skynet.Core.Bootstrap.Steps;

var bootstrapper = new Bootstrapper();

var steps = new IBootStep[]
{
    new BarrierBootStep(
        RuntimeLevel.Bootstrap,
        RuntimeLevel.Core,
        // Null übergeben: Step verwendet seinen Default ("./bootstrap")
        [new BootstrapLoggingStep(bootstrapDirectory: null)])
};

var sp = await bootstrapper.RunAsync(steps);

var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
logger.LogInformation("Bootstrap abgeschlossen. Level={Level}", bootstrapper.CurrentLevel);

var state = sp.GetRequiredService<BootstrapLoggingState>();
logger.LogInformation("Bootstrap-Verzeichnis schreibbar: {Ok}, Pfad={Dir}", state.CanWriteFiles, state.LogDirectory ?? "<none>");

Console.WriteLine("Done.");