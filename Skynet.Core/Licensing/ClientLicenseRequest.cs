namespace Skynet.Core.Licensing;

public sealed record ClientLicenseRequest(
    string ClientPubEcdhSpkiBase64,
    string NonceClientBase64,
    ClientInitMeta Meta,
    string Kem,   // "ECDH-P256"
    string Kdf,   // "HKDF-SHA256"
    string Aead   // "AES-256-GCM"
);
