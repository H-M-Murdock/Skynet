namespace Skynet.Core.Crypto;

public interface IAead
{
    // Nonce wird intern generiert (empfohlen für die meisten Fälle)
    (byte[] Nonce, byte[] Tag, byte[] Cipher) Encrypt(byte[] key, byte[] plaintext, byte[]? aad = null);

    // Volle Kontrolle (z. B. für deterministische Tests) – erwartet gültige Nonce-/Tag-Längen
    (byte[] Tag, byte[] Cipher) Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[]? aad = null);

    // Entschlüsseln – wirft CryptographicException bei Auth-Tag-Fehlern
    byte[] Decrypt(byte[] key, byte[] nonce, byte[] tag, byte[] cipher, byte[]? aad = null);

    // Async-Varianten für große Daten
    Task<(byte[] Nonce, byte[] Tag, byte[] Cipher)> EncryptAsync(byte[] key, Stream plaintext, byte[]? aad = null, CancellationToken ct = default);
    Task<byte[]> DecryptAsync(byte[] key, byte[] nonce, byte[] tag, Stream cipherStream, byte[]? aad = null, CancellationToken ct = default);

    // Optional: Validierungs-Helpers (kontraktuelle Längen)
    int ExpectedNonceSizeBytes { get; }
    int ExpectedTagSizeBytes { get; }
}