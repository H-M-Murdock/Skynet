using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

public interface ILicenseTools
{
    // 1) Client initiiert: erzeugt PrivC/PubC + NonceC (persistiert bei Bedarf PrivC sicher)
    Task<(byte[] ClientPrivEcdh, byte[] ClientPubEcdh, byte[] NonceClient, ClientInitMeta Meta)>
        ClientBeginAsync(ClientInitMeta meta, CancellationToken ct = default);

    // 2) Server antwortet mit signierter Hülle
    Task<LicenseEnvelope> ServerIssueAsync(byte[] clientPubEcdh, ClientInitMeta clientMeta, ServerInitMeta serverMeta, CancellationToken ct = default);

    // 3) Client finalisiert: K ableiten, DEK generieren, DEKenc bauen, Lizenz speichern
    // HKDF-Info = UTF8($"tenant/{tenantId}/license-v1"), HKDF-Salt = NonceClient||NonceServer
    // AAD (DEK-Wrap) = UTF8($"tenant/{tenantId};purpose=license;v=1")
    Task<LicenseEnvelope> ClientFinalizeAsync(
        LicenseEnvelope serverEnvelope,
        byte[] clientPrivEcdh,
        ISecretStore store,
        TenantId tenantId,
        CancellationToken ct = default);

    // Renewal (ohne DEK-Rotation): neue Hülle, K’ ableiten, DEK neu verpacken
    Task<LicenseEnvelope> RenewAsync(
        LicenseEnvelope current,
        byte[] clientPrivEcdh,
        ServerInitMeta newServerMeta,
        ISecretStore store,
        TenantId tenantId,
        CancellationToken ct = default);

    // Rotation (mit neuem DEK): neuen DEK erzeugen, rekeying orchestrieren
    Task<(LicenseEnvelope NewLicense, byte[] NewDek)> RotateDekAsync(
        LicenseEnvelope current,
        byte[] clientPrivEcdh,
        ServerInitMeta newServerMeta,
        ISecretStore store,
        TenantId tenantId,
        Func<string, Task<byte[]?>> readAndDecryptWithOldDek,
        Func<string, byte[], Task> encryptAndWriteWithNewDek,
        CancellationToken ct = default);
}