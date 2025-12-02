namespace Skynet.Core.Licensing;

public sealed class LicensingOptions
{
    /// <summary>
    /// Der öffentliche Schlüssel (Ed25519) des Lizenzservers im Base64-Format.
    /// Dieser wird benötigt, um die Signatur der LicenseEnvelope zu validieren.
    /// </summary>
    public string ServerVerifyKeyBase64 { get; set; } = string.Empty;
}
