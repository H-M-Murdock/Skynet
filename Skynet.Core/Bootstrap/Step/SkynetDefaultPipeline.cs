
namespace Skynet.Core.Bootstrap;

/// <summary>
/// Definiert den Standard-Startvorgang des Frameworks.
/// </summary>
internal static class SkynetDefaultPipeline
{
    public static void ConfigureDefaults(Bootstrapper bootstrapper)
    {
        bootstrapper.AddStep(new InitTimeServicesStep());
        bootstrapper.AddStep(new RegisterCryptoServicesStep());
        
        bootstrapper.AddStep(new InitFilesystemStep()); 
        bootstrapper.AddStep(new EnableBootstrapLoggingStep());
        
        bootstrapper.AddStep(new RegisterResourceProviderStep());
        bootstrapper.AddStep(new RegisterTenantServicesStep());
        
        bootstrapper.AddStep(new RegisterLocalizationServicesStep());
        bootstrapper.AddStep(new RegisterI18NServicesStep());
        
        bootstrapper.AddStep(new RegisterLoggingStackStep());
    }
}
