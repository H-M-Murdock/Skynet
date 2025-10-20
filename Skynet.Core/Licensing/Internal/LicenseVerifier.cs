using System.Security.Cryptography;
using Skynet.Core.Crypto;
using Skynet.Core.Time;

namespace Skynet.Core.Licensing;

/// <summary>
/// Prüft LicenseEnvelope mittels ISignature (Server-Verify-Key) und IClock (Zeitfenster),
/// mappt das Ergebnis in eine domänische LicenseInfo.
/// Hinweis: Die Kanonisierung der zu signierenden Daten erfolgt ausschließlich über ILicenseCanonicalizer.
/// </summary>
public sealed class LicenseVerifier : ILicenseVerifier
{
    private readonly ISignature _signature;
    private readonly byte[] _verifyPublicKey;
    private readonly IClock _clock;
    private readonly ILicenseCanonicalizer _canonicalizer;

    public LicenseVerifier(ISignature signature, byte[] verifyPublicKey, IClock clock, ILicenseCanonicalizer canonicalizer)
    {
        _signature = signature ?? throw new ArgumentNullException(nameof(signature));
        _verifyPublicKey = verifyPublicKey ?? throw new ArgumentNullException(nameof(verifyPublicKey));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _canonicalizer = canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));
    }

    public Task<LicenseInfo> VerifyAsync(
        LicenseEnvelope envelope,
        IReadOnlyDictionary<string, string>? defaultClaims = null,
        bool requireValidSignature = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // 1) Zeitfenster prüfen
        var now = _clock.UtcNow;
        if (envelope.NotBeforeUtc is { } nbf && now < nbf)
            throw new CryptographicException("License not yet valid (NotBefore).");
        if (now > envelope.ExpiresUtc)
            throw new CryptographicException("License expired.");

        // 2) Kanonischen Signatur-Input herstellen (alle Felder außer Signature)
        var dataToVerify = _canonicalizer.CanonicalizeWithoutSignature(envelope);

        // 3) Signatur verifizieren
        var isValid = false;
        try
        {
            isValid = _signature.Verify(_verifyPublicKey, dataToVerify, envelope.Signature);
        }
        catch (ArgumentException ex)
        {
            throw new CryptographicException("Signature verification failed due to invalid inputs.", ex);
        }

        if (requireValidSignature && !isValid)
            throw new CryptographicException("License signature invalid.");

        // 4) Claims/Metadaten übernehmen
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (defaultClaims is not null)
        {
            foreach (var kv in defaultClaims) claims[kv.Key] = kv.Value;
        }
        if (envelope.FeatureFlags is not null)
        {
            foreach (var kv in envelope.FeatureFlags) claims[kv.Key] = kv.Value;
        }

        // 5) Fingerprint (Audit/ETag) über kanonisierte Daten
        var fingerprint = ComputeSha256Hex(dataToVerify);

        // 6) Ergebnis erzeugen
        var info = new LicenseInfo(
            tenantId: envelope.TenantId,
            issuedAtUtc: envelope.IssuedAtUtc,
            expiresUtc: envelope.ExpiresUtc,
            claims: claims,
            notBeforeUtc: envelope.NotBeforeUtc,
            licenseId: null,
            policyName: null,
            signatureValid: isValid,
            signKeyId: envelope.SignKeyId,
            envelopeFingerprint: fingerprint,
            rawLicense: null
        );

        return Task.FromResult(info);
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data));
    }
}
