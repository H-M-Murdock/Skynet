using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

/// <summary>Key Encapsulation Mechanism (Schlüsselaustausch).</summary>
public enum KemAlgorithm 
{ 
    /// <summary>X25519 (Curve25519 Diffie-Hellman).</summary>
    X25519 = 0 
    /*, P256 = 1*/ 
}

/// <summary>Key Derivation Function (Schlüsselableitung).</summary>
public enum KdfAlgorithm 
{ 
    /// <summary>HMAC-based Key Derivation Function mit SHA-256.</summary>
    HkdfSha256 = 0 
    /*, HkdfSha512 = 1*/ 
}

/// <summary>Authenticated Encryption with Associated Data (Verschlüsselung).</summary>
public enum AeadAlgorithm 
{ 
    /// <summary>AES-256 im Galois/Counter Mode.</summary>
    Aes256Gcm = 0 
    /*, ChaCha20Poly1305 = 1*/ 
}

/// <summary>
/// Die kryptografische Hülle ("Umschlag") einer Lizenz.
/// Enthält alle Daten, die für den Schlüsselaustausch (ECDH -> HKDF -> SessionKey)
/// und die Validierung (Signatur, Zeitfenster) notwendig sind.
/// <para>
/// Wichtig: Enthält KEINE verschlüsselten Nutzdaten (wie den DEK). 
/// Der DEK wird clientseitig aus dem abgeleiteten SessionKey gebildet/gewrappt.
/// </para>
/// </summary>
public sealed record LicenseEnvelope
{
    /// <summary>
    /// Die ID des Mandanten, für den die Lizenz ausgestellt wurde.
    /// Wird in AAD und HKDF-Info kryptografisch gebunden.
    /// </summary>
    public TenantId TenantId { get; }

    /// <summary>
    /// Der ephemere öffentliche Schlüssel des Servers (für ECDH).
    /// </summary>
    public byte[] ServerPubEcdh { get; }

    /// <summary>
    /// Zufallszahl des Clients (Bindung an Handshake).
    /// Schützt vor Replay-Angriffen. Empfohlene Länge: 12 Bytes (96 Bit) oder mehr.
    /// </summary>
    public byte[] NonceClient { get; }

    /// <summary>
    /// Zufallszahl des Servers (Bindung an Handshake).
    /// Erhöht die Entropie des abgeleiteten Schlüssels. Empfohlene Länge: 12 Bytes (96 Bit) oder mehr.
    /// </summary>
    public byte[] NonceServer { get; }

    /// <summary>
    /// Liste der aktivierten Features oder Limits (Claims).
    /// </summary>
    public IReadOnlyDictionary<string, string>? FeatureFlags { get; }

    /// <summary>
    /// Zeitpunkt der Ausstellung (UTC).
    /// </summary>
    public DateTimeOffset IssuedAtUtc { get; }

    /// <summary>
    /// Optionaler Startzeitpunkt der Gültigkeit (UTC).
    /// </summary>
    public DateTimeOffset? NotBeforeUtc { get; }

    /// <summary>
    /// Zeitpunkt des Ablaufs (UTC).
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; }

    /// <summary>
    /// Der verwendete Key Encapsulation Mechanism (z. B. X25519).
    /// </summary>
    public KemAlgorithm KemAlg { get; }

    /// <summary>
    /// Die verwendete Key Derivation Function (z. B. HKDF-SHA256).
    /// </summary>
    public KdfAlgorithm KdfAlg { get; }

    /// <summary>
    /// Der verwendete AEAD-Algorithmus (z. B. AES-256-GCM).
    /// </summary>
    public AeadAlgorithm AeadAlg { get; }

    /// <summary>
    /// Optionale ID des verwendeten Signaturschlüssels (kid), um Schlüsselrotation zu unterstützen.
    /// </summary>
    public string? SignKeyId { get; }

    /// <summary>
    /// Die Signatur des Servers über alle vorangehenden Felder.
    /// Algorithmus der Signatur (z.B. Ed25519) wird implizit durch den Verifier bestimmt.
    /// </summary>
    public byte[] Signature { get; }

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
    
        // Validierung der Zeitfenster-Logik
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