using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

/// <summary>
/// Verifiziert eine LicenseEnvelope (Signatur, Zeitfenster) und mappt auf LicenseInfo.
/// </summary>
public interface ILicenseVerifier
{
    /// <summary>
    /// Verifiziert die Envelope und gibt eine LicenseInfo zurück (SignatureValid, Claims, Zeiten).
    /// Wirft ArgumentException bei inkonsistenten Eingaben.
    /// Wirft CryptographicException bei Signatur-Fehlern, wenn requireValidSignature = true.
    /// </summary>
    Task<LicenseInfo> VerifyAsync(
        LicenseEnvelope envelope,
        IReadOnlyDictionary<string, string>? defaultClaims = null,
        bool requireValidSignature = true,
        CancellationToken ct = default);
}
