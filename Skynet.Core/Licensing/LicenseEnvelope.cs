using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

public enum KemAlgorithm { X25519 = 0 /*, P256 = 1*/ }
public enum KdfAlgorithm { HkdfSha256 = 0 /*, HkdfSha512 = 1*/ }
public enum AeadAlgorithm { Aes256Gcm = 0 /*, ChaCha20Poly1305 = 1*/ }

public sealed record LicenseEnvelope
{
    public TenantId TenantId { get; }
    public byte[] ServerPubEcdh { get; }
    public byte[] NonceClient { get; }
    public byte[] NonceServer { get; }
    public IReadOnlyDictionary<string, string>? FeatureFlags { get; }
    public DateTimeOffset IssuedAtUtc { get; }
    public DateTimeOffset? NotBeforeUtc { get; }
    public DateTimeOffset ExpiresUtc { get; }
    public KemAlgorithm KemAlg { get; }      // z. B. X25519
    public KdfAlgorithm KdfAlg { get; }      // z. B. HKDF-SHA256
    public AeadAlgorithm AeadAlg { get; }    // z. B. AES-256-GCM
    public string? SignKeyId { get; }        // optional: kid für Verify-Key-Rotation
    public byte[] Signature { get; }         // Sig über alle Felder außer Signature

    public LicenseEnvelope(
        TenantId tenantId,
        byte[] serverPubEcdh,
        byte[] nonceClient,
        byte[] nonceServer,
        IReadOnlyDictionary<string, string>? featureFlags,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset? notBeforeUtc,
        DateTimeOffset expiresUtc,
        KemAlgorithm kemAlg,
        KdfAlgorithm kdfAlg,
        AeadAlgorithm aeadAlg,
        string? signKeyId,
        byte[] signature)
    {
        TenantId = tenantId;

        ServerPubEcdh = serverPubEcdh ?? throw new ArgumentNullException(nameof(serverPubEcdh));
        NonceClient = nonceClient ?? throw new ArgumentNullException(nameof(nonceClient));
        NonceServer = nonceServer ?? throw new ArgumentNullException(nameof(nonceServer));
        Signature = signature ?? []; // Signatur kann beim Aufbau zunächst leer sein

        if (ServerPubEcdh.Length == 0) throw new ArgumentException("ServerPubEcdh must not be empty.", nameof(serverPubEcdh));
        if (NonceClient.Length == 0) throw new ArgumentException("NonceClient must not be empty.", nameof(nonceClient));
        if (NonceServer.Length == 0) throw new ArgumentException("NonceServer must not be empty.", nameof(nonceServer));
        if (expiresUtc <= issuedAtUtc) throw new ArgumentException("ExpiresUtc must be after IssuedAtUtc.", nameof(expiresUtc));
        if (notBeforeUtc is { } nbf && nbf > expiresUtc) throw new ArgumentException("NotBeforeUtc must be <= ExpiresUtc.", nameof(notBeforeUtc));

        FeatureFlags = featureFlags;
        IssuedAtUtc = issuedAtUtc;
        NotBeforeUtc = notBeforeUtc;
        ExpiresUtc = expiresUtc;
        KemAlg = kemAlg;
        KdfAlg = kdfAlg;
        AeadAlg = aeadAlg;
        SignKeyId = signKeyId;
    }
}