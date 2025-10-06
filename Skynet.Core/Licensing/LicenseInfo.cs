using Skynet.Core.Tenant;
using Skynet.Core.Time;

namespace Skynet.Core.Licensing;

/// <summary>
/// Domänenmodell einer ausgewerteten (verifizierten) Lizenz.
/// Enthält nur fachliche Informationen, keine Krypto-Transportdetails.
/// </summary>
public sealed class LicenseInfo
{
    public TenantId TenantId { get; }
    public string? LicenseId { get; }
    public string? PolicyName { get; }

    /// <summary>UTC-Zeit, zu der die Lizenz ausgestellt wurde.</summary>
    public DateTimeOffset IssuedAtUtc { get; }

    /// <summary>UTC-Zeit, ab der die Lizenz gültig ist (optional).</summary>
    public DateTimeOffset? NotBeforeUtc { get; }

    /// <summary>UTC-Zeit, zu der die Lizenz abläuft.</summary>
    public DateTimeOffset ExpiresUtc { get; }

    /// <summary>Vom Server signierte Feature-/Limit-Claims als key/value.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; }

    /// <summary>
    /// Ergebnis der Signaturprüfung der zugehörigen Hülle (Server-Signatur).
    /// true = Signatur gültig; false = ungültig; null = nicht geprüft.
    /// </summary>
    public bool? SignatureValid { get; }

    /// <summary>
    /// Optional: Kennung des Signatur-Schlüssels (kid), der zur Verifikation genutzt wurde.
    /// </summary>
    public string? SignKeyId { get; }

    /// <summary>
    /// Optional: Hash/Fingerprint der verifizierten Hülle (Audit/ETag).
    /// </summary>
    public string? EnvelopeFingerprint { get; }

    /// <summary>
    /// Optional: Rohinhalt der Lizenz (z. B. kanonisiertes JSON) für Audits/Debugging.
    /// </summary>
    public string? RawLicense { get; }

    public LicenseInfo(
        TenantId tenantId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresUtc,
        IReadOnlyDictionary<string, string>? claims = null,
        DateTimeOffset? notBeforeUtc = null,
        string? licenseId = null,
        string? policyName = null,
        bool? signatureValid = null,
        string? signKeyId = null,
        string? envelopeFingerprint = null,
        string? rawLicense = null)
    {
        if (expiresUtc <= issuedAtUtc)
            throw new ArgumentException("ExpiresUtc must be after IssuedAtUtc.", nameof(expiresUtc));
        if (notBeforeUtc is { } nbf && nbf > expiresUtc)
            throw new ArgumentException("NotBeforeUtc must be <= ExpiresUtc.", nameof(notBeforeUtc));

        TenantId = tenantId;
        IssuedAtUtc = issuedAtUtc;
        ExpiresUtc = expiresUtc;
        NotBeforeUtc = notBeforeUtc;
        Claims = claims ?? new Dictionary<string, string>();
        LicenseId = licenseId;
        PolicyName = policyName;
        SignatureValid = signatureValid;
        SignKeyId = signKeyId;
        EnvelopeFingerprint = envelopeFingerprint;
        RawLicense = rawLicense;
    }

    /// <summary>
    /// true, wenn (optional) Signatur gültig ist und Zeitfenster erfüllt ist.
    /// Wenn SignatureValid null ist, wird nur das Zeitfenster geprüft.
    /// </summary>
    public bool IsValid(IClock? clock = null, bool requireSignature = false)
    {
        var now = (clock?.UtcNow ?? DateTimeOffset.UtcNow);
        if (requireSignature && SignatureValid != true) return false;
        if (now < (NotBeforeUtc ?? DateTimeOffset.MinValue)) return false;
        if (now > ExpiresUtc) return false;
        return true;
    }

    /// <summary>
    /// Tage bis zum Ablauf (negativ wenn bereits abgelaufen).
    /// </summary>
    public double DaysUntilExpiry(IClock? clock = null)
    {
        var now = (clock?.UtcNow ?? DateTimeOffset.UtcNow);
        return (ExpiresUtc - now).TotalDays;
    }

    /// <summary>
    /// Liefert einen Claim-Wert oder null, wenn nicht vorhanden.
    /// </summary>
    public string? GetClaim(string key)
        => Claims.TryGetValue(key, out var v) ? v : null;

    /// <summary>
    /// Liefert einen Claim als bool (true/false), defaultValue falls nicht vorhanden/ungültig.
    /// </summary>
    public bool GetClaimBool(string key, bool defaultValue = false)
    {
        if (!Claims.TryGetValue(key, out var v)) return defaultValue;
        return bool.TryParse(v, out var b) ? b : defaultValue;
    }

    /// <summary>
    /// Liefert einen Claim als int, defaultValue falls nicht vorhanden/ungültig.
    /// </summary>
    public int GetClaimInt(string key, int defaultValue = 0)
    {
        if (!Claims.TryGetValue(key, out var v)) return defaultValue;
        return int.TryParse(v, out var i) ? i : defaultValue;
    }
}
