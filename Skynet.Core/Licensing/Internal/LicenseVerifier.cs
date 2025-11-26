using System.Security.Cryptography;
using Skynet.Core.Crypto;
using Skynet.Core.Time;

namespace Skynet.Core.Licensing;

/// <summary>
/// Standard-Implementierung des <see cref="ILicenseVerifier"/>.
/// Prüft eine <see cref="LicenseEnvelope"/> mittels <see cref="ISignature"/> (Server-Verify-Key) und <see cref="IClock"/> (Zeitfenster).
/// Mappt das Ergebnis bei Erfolg in eine domänische <see cref="LicenseInfo"/>.
/// <para>
/// Ablauf:
/// 1. Zeitfenster-Prüfung (NotBefore / Expires).
/// 2. Kanonisierung der Envelope (ohne Signatur) mittels <see cref="ILicenseCanonicalizer"/>.
/// 3. Kryptografische Verifikation der Signatur (Ed25519 etc.).
/// 4. Erstellung des LicenseInfo-Objekts inkl. Fingerprint.
/// </para>
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

    public async Task<LicenseInfo> VerifyAsync(
        LicenseEnvelope envelope,
        IReadOnlyDictionary<string, string>? defaultClaims = null,
        bool requireValidSignature = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // 1) Zeitfenster prüfen
        var now = _clock.UtcNow;
        
        // Glossar-Konform: NotBeforeUtc <= now <= ExpiresUtc
        if (envelope.NotBeforeUtc is { } nbf && now < nbf)
            throw new CryptographicException($"License not yet valid (NotBefore: {nbf:O}, Now: {now:O}).");
            
        if (now > envelope.ExpiresUtc)
            throw new CryptographicException($"License expired (Expires: {envelope.ExpiresUtc:O}, Now: {now:O}).");

        // 2) Kanonischen Signatur-Input herstellen (alle Felder außer Signature)
        // Dies stellt sicher, dass Byte-für-Byte dasselbe signiert wird, was der Server signiert hat.
        var dataToVerify = _canonicalizer.CanonicalizeWithoutSignature(envelope);

        // 3) Signatur verifizieren
        var isValid = false;
        try
        {
            // Der _verifyPublicKey muss zum privaten Schlüssel passen, mit dem der Server signiert hat.
            isValid = _signature.Verify(_verifyPublicKey, dataToVerify, envelope.Signature);
        }
        catch (ArgumentException ex)
        {
            throw new CryptographicException("Signature verification failed due to invalid inputs (e.g. wrong key length).", ex);
        }

        if (requireValidSignature && !isValid)
            throw new CryptographicException("License signature invalid. The content may have been tampered with or the key is incorrect.");

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
            licenseId: null, // Könnte später aus Claims extrahiert werden
            policyName: null,
            signatureValid: isValid,
            signKeyId: envelope.SignKeyId,
            envelopeFingerprint: fingerprint,
            rawLicense: null // Hier könnte man ggf. dataToVerify als String speichern, falls benötigt
        );

        return await Task.FromResult(info);
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        // Optimierung: Statische Methode nutzen, spart Allokation des HashAlgorithm-Objekts
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes);
    }
}
