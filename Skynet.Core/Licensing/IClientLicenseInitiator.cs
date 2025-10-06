using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

public interface IClientLicenseInitiator
{
    // Erstnachricht: erstellt PubC + NonceC, hält PrivC im RAM/Store.
    // Optional: kem/kdf/aead als Parameter, falls mehrere Profile unterstützt werden.
    Task<(byte[] ClientPubEcdh, byte[] NonceClient, ClientInitMeta Meta, byte[]? ClientProof)> CreateInitAsync(
        ClientInitMeta meta,
        CancellationToken ct = default);

    // Finalisierung: erhält Server-Hülle, leitet K ab, erzeugt DEK (+DEKenc) und baut finale Lizenz.
    // Wirft CryptographicException bei Verifikations-/KDF-/AEAD-Fehlern.
    Task<LicenseEnvelope> FinalizeAsync(
        LicenseEnvelope serverEnvelope,             // Hülle inkl. PubS/Nonces/Sig (Alg-Infos konsistent)
        byte[] clientPrivateEcdh,                   // PrivC (oder via internem Store)
        byte[]? clientProofSecret = null,           // optional für Mutual-Auth
        CancellationToken ct = default);

    // Ableitung K aus PrivC/PubS + Nonces (falls separat benötigt).
    // Wirft ArgumentException/CryptographicException bei inkorrekten Längen oder Ableitungsfehlern.
    byte[] DeriveSessionKey(
        byte[] clientPrivEcdh,
        byte[] serverPubEcdh,
        byte[] nonceClient,
        byte[] nonceServer,
        TenantId tenantId);

    // Optional: asynchrone Ableitung (für KMS/HSM-Fälle)
    Task<byte[]> DeriveSessionKeyAsync(
        byte[] clientPrivEcdh,
        byte[] serverPubEcdh,
        byte[] nonceClient,
        byte[] nonceServer,
        TenantId tenantId,
        CancellationToken ct = default);
}