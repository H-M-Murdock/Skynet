namespace Skynet.Core.Licensing;

public interface IClientDekManager
{
    // DEK-Erzeugung (clientseitig)
    byte[] GenerateDek(int sizeBytes = 32);

    // Verpacken/Entpacken des DEK via KEK
    // AAD/Context bindet den DEK an Tenant/Key-Name usw. (Empfehlung: TenantId/Policy-Hash)
    byte[] WrapDek(byte[] kek, byte[] dek, byte[]? aad = null);
    byte[] UnwrapDek(byte[] kek, byte[] dekEnc, byte[]? aad = null);

    // Async-Varianten (optional, falls DEK groß/Streaming später gewünscht):
    Task<byte[]> WrapDekAsync(byte[] kek, byte[] dek, byte[]? aad = null, CancellationToken ct = default);
    Task<byte[]> UnwrapDekAsync(byte[] kek, byte[] dekEnc, byte[]? aad = null, CancellationToken ct = default);

    // ReKeying-Prozess (Dual-Read/Single-Write)
    // Liefert eine kleine Statistik zurück.
    Task<RekeyResult> RekeyAllAsync(
        ISecretStore store,
        Func<string, Task<byte[]?>> decryptWithOldDek,   // liefert Klartext
        Func<string, byte[], Task> encryptWithNewDek,    // schreibt neuen Cipher
        CancellationToken ct = default);
}

// Ergebnisobjekt für ReKeying – erleichtert Monitoring/Logs
public sealed record RekeyResult(
    int Total,
    int Migrated,
    int Skipped,
    int Failed,
    IReadOnlyList<string>? FailedKeys = null);