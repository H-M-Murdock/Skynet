using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Tenant;
using Skynet.Core.Time;

namespace Skynet.Core.Licensing;

/// <summary>
/// Prüft LicenseEnvelope mittels ISignature (Server-Verify-Key) und IClock (Zeitfenster),
/// mappt das Ergebnis in eine domänische LicenseInfo.
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

        // 1) Zeitfenster grob prüfen (vor Signatur, um frühe Fehler zu erkennen)
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
            // Key-/Datenlängen-Fehler
            throw new CryptographicException("Signature verification failed due to invalid inputs.", ex);
        }

        if (requireValidSignature && !isValid)
            throw new CryptographicException("License signature invalid.");

        // 4) Claims/Metadaten übernehmen (FeatureFlags → Claims)
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (defaultClaims is not null)
        {
            foreach (var kv in defaultClaims) claims[kv.Key] = kv.Value;
        }
        if (envelope.FeatureFlags is not null)
        {
            foreach (var kv in envelope.FeatureFlags) claims[kv.Key] = kv.Value;
        }

        // 5) Optional Fingerprint/Audit-Hash über die (ohne Signature) kanonisierten Daten
        var fingerprint = ComputeSha256Hex(dataToVerify);

        // 6) LicenseInfo erstellen
        var info = new LicenseInfo(
            tenantId: envelope.TenantId,
            issuedAtUtc: envelope.IssuedAtUtc,
            expiresUtc: envelope.ExpiresUtc,
            claims: claims,
            notBeforeUtc: envelope.NotBeforeUtc,
            licenseId: null,                     // optional aus Claims/Policy ableiten
            policyName: null,                    // optional aus Claims ableiten
            signatureValid: isValid,
            signKeyId: envelope.SignKeyId,
            envelopeFingerprint: fingerprint,
            rawLicense: null                     // optional: Original-JSON/CBOR, falls verfügbar
        );

        return Task.FromResult(info);
    }

    private static byte[] BuildCanonicalData(LicenseEnvelope e)
    {
        // Reihenfolge ist entscheidend: MUSS auf Server und Client identisch sein.
        // Beispiel: UTF-8-Concatenation mit Längenpräfixen zur Eindeutigkeit.
        // Format: |TID|PubS|NonceC|NonceS|Issued|Nbf|Exp|Kem|Kdf|Aead|kid|flags-json|
        // Für Produktionssysteme eher: kanonisches JSON/CBOR verwenden.

        var sb = new StringBuilder(256);

        void AppendBytes(string label, byte[] data)
        {
            sb.Append(label).Append(':').Append(data.Length).Append(':');
            // bytes als Base64 – eindeutig
            sb.Append(Convert.ToBase64String(data)).Append('|');
        }

        void AppendString(string label, string? s)
        {
            s ??= string.Empty;
            sb.Append(label).Append(':').Append(s.Length).Append(':').Append(s).Append('|');
        }

        void AppendDate(string label, DateTimeOffset dto)
        {
            // RFC3339/ISO8601
            AppendString(label, dto.UtcDateTime.ToString("O"));
        }

        // Pflichtfelder
        AppendString("tid", e.TenantId.ToString());
        AppendBytes("pubs", e.ServerPubEcdh);
        AppendBytes("nc", e.NonceClient);
        AppendBytes("ns", e.NonceServer);
        AppendDate("iss", e.IssuedAtUtc);
        if (e.NotBeforeUtc.HasValue)
            AppendDate("nbf", e.NotBeforeUtc.Value);
        AppendDate("exp", e.ExpiresUtc);
        AppendString("kem", e.KemAlg.ToString());
        AppendString("kdf", e.KdfAlg.ToString());
        AppendString("aead", e.AeadAlg.ToString());
        AppendString("kid", e.SignKeyId);

        // FeatureFlags deterministisch serialisieren
        if (e.FeatureFlags is { Count: > 0 })
        {
            var ordered = e.FeatureFlags.OrderBy(kv => kv.Key, StringComparer.Ordinal);
            var flags = new StringBuilder();
            foreach (var kv in ordered)
            {
                flags.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            }
            AppendString("flags", flags.ToString());
        }
        else
        {
            AppendString("flags", string.Empty);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data));
    }
}
