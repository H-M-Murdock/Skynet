namespace Skynet.Core.Licensing;

/// <summary>
/// Verwaltet Data Encryption Keys (DEKs) auf Client-Seite.
/// Zuständig für Erzeugung, Wrapping (Verschlüsselung mit Session-Key/KEK) und Rotation (ReKeying).
/// </summary>
public interface IClientDekManager
{
    /// <summary>
    /// Erzeugt einen kryptografisch sicheren Zufallsschlüssel (DEK).
    /// </summary>
    /// <param name="sizeBytes">Gewünschte Länge in Bytes (Standard 32 für AES-256).</param>
    byte[] GenerateDek(int sizeBytes = 32);

    /// <summary>
    /// Verschlüsselt (wrappt) einen DEK mit einem Key Encryption Key (KEK).
    /// <para>
    /// Projektweite Konvention für AAD: <c>UTF8("tenant/{TenantId};purpose=license;v=1")</c>.
    /// </para>
    /// </summary>
    /// <param name="kek">Der Schlüssel zum Einpacken (z. B. abgeleiteter Session-Key).</param>
    /// <param name="dek">Der zu schützende Datenschlüssel (Klartext).</param>
    /// <param name="aad">Zusätzliche authentifizierte Daten zur Kontextbindung.</param>
    /// <returns>Verpackter Schlüssel (z. B. Nonce + Ciphertext + Tag).</returns>
    byte[] WrapDek(byte[] kek, byte[] dek, byte[]? aad = null);

    /// <summary>
    /// Entschlüsselt (unwrappt) einen verpackten DEK mit dem KEK.
    /// Wirft CryptographicException bei Validierungsfehlern (falscher Key/Tag/AAD).
    /// </summary>
    byte[] UnwrapDek(byte[] kek, byte[] dekEnc, byte[]? aad = null);

    // Hinweis: Async-Methoden für reine CPU-Krypto (ohne HSM) entfernt, 
    // da sie Overhead erzeugen und falsche Erwartungen (I/O) wecken.

    /// <summary>
    /// Führt eine Rotation (Re-Encryption) aller Secrets in einem Store durch.
    /// Nutzt Dual-Read/Single-Write Logik oder einfache Migration, je nach Callback-Implementierung.
    /// </summary>
    /// <param name="store">Der Secret-Store, über den iteriert werden soll (optional, falls Callbacks reichen).</param>
    /// <param name="decryptWithOldDek">Funktion zum Entschlüsseln eines Eintrags mit dem alten DEK.</param>
    /// <param name="encryptWithNewDek">Funktion zum Verschlüsseln und Speichern mit dem neuen DEK.</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Statistik über den Migrationslauf.</returns>
    Task<RekeyResult> RekeyAllAsync(
        ISecretStore store,
        Func<string, Task<byte[]?>> decryptWithOldDek,   // liefert Klartext
        Func<string, byte[], Task> encryptWithNewDek,    // schreibt neuen Cipher
        CancellationToken ct = default);
}

/// <summary>
/// Ergebnis einer Key-Rotation / Migration.
/// </summary>
/// <param name="Total">Anzahl bearbeiteter Schlüssel.</param>
/// <param name="Migrated">Erfolgreich migriert.</param>
/// <param name="Skipped">Übersprungen (z. B. bereits neuer Key).</param>
/// <param name="Failed">Fehlgeschlagen.</param>
/// <param name="FailedKeys">Liste der Keys, bei denen Fehler auftraten.</param>
public sealed record RekeyResult(
    int Total,
    int Migrated,
    int Skipped,
    int Failed,
    IReadOnlyList<string>? FailedKeys = null);