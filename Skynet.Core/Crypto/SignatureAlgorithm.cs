namespace Skynet.Core.Crypto;

public enum SignatureAlgorithm
{
    // Ed25519 ist ohne Zusatzbibliothek in der .NET-BCL nicht verfügbar.
    // Ed25519 = 0,

    /// <summary>
    /// ECDSA über NIST P-256 (secp256r1). In .NET BCL nativ verfügbar,
    /// breit interoperabel (PKI/HSM/Browser). Schlüssel-Export: PKCS#8 (privat), SPKI (öffentlich).
    /// </summary>
    ECDSA_P256 = 1
}