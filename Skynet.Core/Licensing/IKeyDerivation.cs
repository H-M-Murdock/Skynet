namespace Skynet.Core.Licensing;

/// <summary>
/// Key-Derivation (HKDF) für symmetrische Schlüsselableitungen.
/// Rückgabe ist abgeleitetes Keymaterial; Eingaben werden validiert.
/// </summary>
public interface IKeyDerivation
{
    /// <summary>
    /// HKDF-SHA256: leitet aus IKM (Input Keying Material) mit optionalem Salt/Info einen Schlüssel ab.
    /// Wirft ArgumentException bei ungültigen Parametern (null/leere Arrays, lengthBytes &lt;= 0).
    /// </summary>
    /// <param name="ikm">Input Keying Material (z. B. ECDH-Shared-Secret)</param>
    /// <param name="salt">Optionales Salt (kann null/leer sein)</param>
    /// <param name="info">Optionaler Kontext-String (kann null/leer sein), z. B. "tenant/{id}/license"</param>
    /// <param name="lengthBytes">Länge des abzuleitenden Schlüssels (z. B. 32 für AES-256)</param>
    /// <returns>Abgeleiteter Schlüssel der gewünschten Länge.</returns>
    byte[] HkdfSha256(byte[] ikm, byte[]? salt, byte[]? info, int lengthBytes = 32);

    /// <summary>
    /// Optional: HKDF-SHA512 (falls benötigt).
    /// </summary>
    byte[] HkdfSha512(byte[] ikm, byte[]? salt, byte[]? info, int lengthBytes);
}