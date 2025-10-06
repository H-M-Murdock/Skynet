using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

public sealed record ServerInitMeta
{
    public TenantId TenantId { get; }
    public IReadOnlyDictionary<string, string>? FeatureFlags { get; }
    public DateTimeOffset IssuedAtUtc { get; }
    public DateTimeOffset? NotBeforeUtc { get; }
    public DateTimeOffset ExpiresUtc { get; }

    public string? LicenseId { get; }
    public string? PolicyName { get; }
    public string? SignKeyId { get; } // kid für Verify-Key-Rotation
    public int Version { get; } = 1;

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