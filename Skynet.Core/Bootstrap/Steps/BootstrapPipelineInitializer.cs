// Skynet.Core.Bootstrap/Steps/BootstrapPipeline.cs


namespace Skynet.Core.Bootstrap;


public static class BootstrapPipeline
{
    public static IEnumerable<IBootStep> CreateInitialPipeline(string? bootstrapDir = null)
    {
        // 1) Bootstrap -> Core: generelle Bootstrap-Dienste + System-Ressourcen registrieren
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
                new RegisterSystemTenantResourcesStep() // Ressourcen stehen ab Core bereit
            });

        // 2) Core -> Init: System-Tenant-Context registrieren (nutzt bereits Ressourcen)
        yield return new BarrierBootStep(
            RuntimeLevel.Core,
            RuntimeLevel.Init,
            new IBootStep[]
            {
                new RegisterSystemTenantContextStep(),
                new RegisterLocalAesProtectorStep()
            });
    }
}
