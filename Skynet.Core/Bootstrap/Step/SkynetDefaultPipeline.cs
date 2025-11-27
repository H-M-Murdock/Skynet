
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
    }
}
