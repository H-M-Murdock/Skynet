using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

/// <summary>
/// Enthält die fachlichen Parameter für die Lizenzausstellung durch den Server.
/// Diese Daten werden von der Lizenzverwaltung (Business Logic) festgelegt und
/// an den <see cref="IServerLicenseIssuer"/> übergeben, um die kryptografische Hülle zu erzeugen.
/// </summary>
public sealed record ServerInitMeta
{
    /// <summary>
    /// Die Tenant-ID, für die die Lizenz gilt.
    /// </summary>
    public TenantId TenantId { get; }

    /// <summary>
    /// Liste der Features und Limits (Claims), die in die Lizenz eingebettet werden.
    /// </summary>
    public IReadOnlyDictionary<string, string>? FeatureFlags { get; }

    /// <summary>
    /// Zeitpunkt der Ausstellung (in der Regel DateTimeOffset.UtcNow).
    /// </summary>
    public DateTimeOffset IssuedAtUtc { get; }

    /// <summary>
    /// Optionaler Startzeitpunkt der Gültigkeit (null = sofort gültig ab Ausstellung).
    /// </summary>
    public DateTimeOffset? NotBeforeUtc { get; }

    /// <summary>
    /// Zeitpunkt des Ablaufs der Lizenz.
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; }

    /// <summary>
    /// Optionale eindeutige Lizenz-ID (z. B. aus Datenbank) für Tracking/Revocation.
    /// </summary>
    public string? LicenseId { get; }

    /// <summary>
    /// Name der verwendeten Lizenz-Policy (z. B. "Trial", "Enterprise-2025").
    /// </summary>
    public string? PolicyName { get; }

    /// <summary>
    /// ID des Signaturschlüssels (kid), falls Key-Rotation verwendet wird.
    /// </summary>
    public string? SignKeyId { get; }

    /// <summary>
    /// Lizenz-Schema-Version (Standard: 1).
    /// </summary>
    public int Version { get; }

    public ServerInitMeta(
        TenantId tenantId,
        IReadOnlyDictionary<string, string>? featureFlags,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset? notBeforeUtc,
        DateTimeOffset expiresUtc,
        string? licenseId = null,
        string? policyName = null,
        string? signKeyId = null,
        int version = 1
        )
    {
        TenantId = tenantId;
        FeatureFlags = featureFlags;
        IssuedAtUtc = issuedAtUtc;
        NotBeforeUtc = notBeforeUtc;
        ExpiresUtc = expiresUtc;
        LicenseId = licenseId;
        PolicyName = policyName;
        SignKeyId = signKeyId;
        Version = version;

        if (expiresUtc <= issuedAtUtc)
            throw new ArgumentException("ExpiresUtc must be after IssuedAtUtc.", nameof(expiresUtc));
        
        if (notBeforeUtc is { } nbf && nbf > expiresUtc)
            throw new ArgumentException("NotBeforeUtc must be <= ExpiresUtc.", nameof(notBeforeUtc));
    }
}