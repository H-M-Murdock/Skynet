// Skynet.Core.Bootstrap/Steps/BootstrapPipeline.cs


namespace Skynet.Core.Bootstrap;


public static class BootstrapPipeline
{
    public static IEnumerable<IBootStep> CreateInitialPipeline(string? bootstrapDir = null)
    {
        // 1) Bootstrap -> Init: generelle Bootstrap-Dienste + System-Ressourcen registrieren
        yield return new BarrierBootStep(
            RuntimeLevel.Bootstrap,
            RuntimeLevel.Init,
            new IBootStep[]
            {
                new BootstrapLoggingStep(bootstrapDirectory: bootstrapDir ?? "./bootstrap/log"),
                new BootstrapLifecycleStep(),
                new BootstrapClockStep(),
                new BootstrapConfigStep(),
                new BootstrapTempStep(appSubfolderName: "Skynet"),
                new BootstrapVersionStep(),
                new RegisterSystemTenantResourcesStep(), // Ressourcen stehen ab Core bereit
                new BootstrapCryptoServicesStep()
                
            });

        // 2) Init -> Core: System-Tenant-Context registrieren (nutzt bereits Ressourcen)
        yield return new BarrierBootStep(
            RuntimeLevel.Init,
            RuntimeLevel.Core,
            new IBootStep[]
            {
                new RegisterSystemTenantContextStep(),
                new RegisterLocalAesProtectorStep(),
                new RegisterAllResourceProvidersStep()
            });
    }
}
