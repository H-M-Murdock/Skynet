using Skynet.Core.Tenant;
using Skynet.Core.Time;

namespace Skynet.Core.Licensing;

/// <summary>
/// Domänenmodell einer ausgewerteten und verifizierten Lizenz.
/// Dient der Anwendungslogik zur Abfrage von Rechten (Claims) und Laufzeiten.
/// </summary>
public sealed class LicenseInfo
{
    /// <summary>Der Lizenznehmer (Mandant).</summary>
    public TenantId TenantId { get; }
    
    /// <summary>Optionale eindeutige ID der Lizenz (Tracking).</summary>
    public string? LicenseId { get; }
    
    /// <summary>Name der zugrundeliegenden Policy.</summary>
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
    /// <c>true</c> = Signatur gültig; <c>false</c> = ungültig; <c>null</c> = nicht geprüft.
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
        
        // Case-Insensitive Dictionary für Claims ist robuster
        var claimsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (claims != null)
        {
            foreach (var kv in claims)
            {
                claimsDict[kv.Key] = kv.Value;
            }
        }
        Claims = claimsDict;

        LicenseId = licenseId;
        PolicyName = policyName;
        SignatureValid = signatureValid;
        SignKeyId = signKeyId;
        EnvelopeFingerprint = envelopeFingerprint;
        RawLicense = rawLicense;
    }

    /// <summary>
    /// Prüft, ob die Lizenz aktuell gültig ist (Zeitfenster).
    /// </summary>
    /// <param name="clock">Optionaler Zeitgeber (sonst DateTimeOffset.UtcNow).</param>
    /// <param name="requireSignature">
    /// Wenn <c>true</c>, muss auch <see cref="SignatureValid"/> auf <c>true</c> stehen.
    /// Standardmäßig <c>false</c>, da oft davon ausgegangen wird, dass ungültige Lizenzen gar nicht erst als LicenseInfo erzeugt werden.
    /// </param>
    public bool IsValid(IClock? clock = null, bool requireSignature = false)
    {
        var now = (clock?.UtcNow ?? DateTimeOffset.UtcNow);
        if (requireSignature && SignatureValid != true) return false;
        if (now < (NotBeforeUtc ?? DateTimeOffset.MinValue)) return false;
        if (now > ExpiresUtc) return false;
        return true;
    }

    /// <summary>
    /// Tage bis zum Ablauf (negativ, wenn bereits abgelaufen).
    /// </summary>
    public double DaysUntilExpiry(IClock? clock = null)
    {
        var now = (clock?.UtcNow ?? DateTimeOffset.UtcNow);
        return (ExpiresUtc - now).TotalDays;
    }

    /// <summary>
    /// Liefert einen Claim-Wert oder null, wenn nicht vorhanden (Key ist case-insensitive).
    /// </summary>
    public string? GetClaim(string key)
        => Claims.TryGetValue(key, out var v) ? v : null;

    /// <summary>
    /// Liefert einen Claim als bool (true/false), defaultValue falls nicht vorhanden/ungültig.
    /// </summary>
    public bool GetClaimBool(string key, bool defaultValue = false)
    {
        if (!Claims.TryGetValue(key, out var v)) return defaultValue;
        // OrdinalIgnoreCase für "True"/"true" Toleranz
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