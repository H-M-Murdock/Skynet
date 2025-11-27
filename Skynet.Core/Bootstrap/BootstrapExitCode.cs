namespace Skynet.Core.Bootstrap;

public enum BootstrapExitCode
{
    Success = 0,
    GeneralError = 1,
    ConfigurationError = 2,
    LicenseError = 3, // Beispiel: Falls LicenseVerifier fehlschlägt
    FileSystemError = 4
}