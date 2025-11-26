using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

/// <summary>
/// Abstrakter Speicher für sicherheitskritische Daten und Lizenzen.
/// Dient als Backend für verschlüsselte Secrets (Ciphertext) und die zugehörige Lizenzdatei.
/// </summary>
public interface ISecretStore
{
    // --- Key-Value Secrets (At-rest encrypted) ---

    /// <summary>
    /// Schreibt ein verschlüsseltes Secret (Cipher-Payload) unter dem angegebenen Schlüssel.
    /// Überschreibt einen existierenden Eintrag (Upsert).
    /// <para>
    /// Hinweis: Der Aufrufer ist verantwortlich für Namespacing (z. B. "tenant-123/db-connection").
    /// </para>
    /// </summary>
    /// <param name="key">Der logische Schlüssel (ggf. mit Pfad/Prefix).</param>
    /// <param name="cipherPayload">Die verschlüsselten Daten (inkl. Nonce/Tag/AAD).</param>
    /// <param name="ct">Cancellation Token.</param>
    Task WriteAsync(string key, byte[] cipherPayload, CancellationToken ct = default);

    /// <summary>
    /// Liest ein verschlüsseltes Secret.
    /// </summary>
    /// <param name="key">Der Schlüssel.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Das verschlüsselte Payload oder <c>null</c>, wenn der Schlüssel nicht existiert.</returns>
    Task<byte[]?> ReadAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Prüft effizient, ob ein Schlüssel existiert, ohne die Daten zu laden.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Löscht ein Secret.
    /// </summary>
    /// <returns><c>true</c>, wenn etwas gelöscht wurde; <c>false</c>, wenn der Key nicht existierte.</returns>
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Listet alle verfügbaren Schlüssel auf, die mit dem optionalen Prefix beginnen.
    /// Wichtig für ReKeying-Prozesse (Iteration über alle Secrets).
    /// </summary>
    /// <param name="prefix">Optionaler Filter (z. B. Tenant-Ordner).</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<IEnumerable<string>> ListKeysAsync(string? prefix = null, CancellationToken ct = default);

    // --- Lizenzverwaltung ---

    /// <summary>
    /// Speichert die Lizenzhülle für einen Mandanten.
    /// Implementierungen entscheiden, wie die Lizenz abgelegt wird (z. B. als "license.json" im Tenant-Ordner).
    /// </summary>
    Task SaveLicenseAsync(LicenseEnvelope license, CancellationToken ct = default);

    /// <summary>
    /// Lädt die aktive Lizenzhülle für einen Mandanten.
    /// </summary>
    /// <returns>Die Lizenzhülle oder <c>null</c>, wenn keine Lizenz gefunden wurde.</returns>
    Task<LicenseEnvelope?> LoadLicenseAsync(TenantId tenantId, CancellationToken ct = default);
}