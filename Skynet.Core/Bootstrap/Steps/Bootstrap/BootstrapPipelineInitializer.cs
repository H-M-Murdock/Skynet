// Skynet.Core.Bootstrap/Steps/BootstrapPipeline.cs


namespace Skynet.Core.Bootstrap;


public static class BootstrapPipeline
{
    public static IEnumerable<IBootStep> CreateInitialPipeline(string? bootstrapDir = null)
    {
        yield return new BarrierBootStep(
            RuntimeLevel.Bootstrap,
            RuntimeLevel.Core,
            new IBootStep[]
            {
                new BootstrapLoggingStep(bootstrapDirectory: bootstrapDir ?? "./bootstrap/log"),
                new BootstrapLifecycleStep(),
                new BootstrapClockStep(),
                new BootstrapConfigStep(),
                new BootstrapTempStep(appSubfolderName: "Skynet"),
                new BootstrapVersionStep(),
                new RegisterSystemTenantResourcesStep()
            });
    }
}
