using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

/// <summary>
/// Persistiert verschlüsselte Secrets (Cipher-Payloads) und die zugehörige Lizenz.
/// Hinweis: Keys sollten tenant-namespaced sein (z. B. "{tenantId}/{key}") oder der Store
/// muss intern pro Tenant separieren, um Kollisionen zu vermeiden.
/// </summary>
public interface ISecretStore
{
    // At-rest: verschlüsselte Einträge (mit DEK) verwalten.
    // Write: definiere im Implementierungs-Doc, ob Overwrite oder Upsert.
    Task WriteAsync(string key, byte[] cipherPayload, CancellationToken ct = default);

    // Read: liefert null, wenn der Key nicht existiert.
    Task<byte[]?> ReadAsync(string key, CancellationToken ct = default);

    // Optional: Existenzabfrage (schneller als Read bei großen Blobs).
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    // Optional: Löschen – hilfreich bei Rotation/Housekeeping.
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);

    // List: optional Prefix-Filter (namespaced Keys), beachte potenziell große Ergebnismengen.
    Task<IEnumerable<string>> ListKeysAsync(string? prefix = null, CancellationToken ct = default);

    // Lizenzverwaltung: Speichern/Laden pro Tenant.
    Task SaveLicenseAsync(LicenseEnvelope license, CancellationToken ct = default);
    Task<LicenseEnvelope?> LoadLicenseAsync(TenantId tenantId, CancellationToken ct = default);
}