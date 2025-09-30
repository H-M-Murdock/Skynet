// Skynet.Core.Bootstrap/Steps/BootstrapPipeline.cs

namespace Skynet.Core.Bootstrap.Steps;

public static class BootstrapPipeline
{
    public static IEnumerable<IBootStep> CreateInitialPipeline(string? bootstrapDir = null)
    {
        yield return new BarrierBootStep(
            RuntimeLevel.Bootstrap,
            RuntimeLevel.Core,
            new IBootStep[]
            {
                new BootstrapLoggingStep(bootstrapDirectory: bootstrapDir),
                new BootstrapClockStep(),
                new BootstrapConfigStep(),
                // nutzt OS-Temp + Unterordner "Skynet"
                new BootstrapTempStep(appSubfolderName: "Skynet")
            });
    }
}
