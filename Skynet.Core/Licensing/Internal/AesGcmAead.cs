using System.Security.Cryptography;

namespace Skynet.Core.Licensing;

/// <summary>
/// Produktive AES-GCM-Implementierung (AEAD) mit
/// - Nonce (IV) = 96 Bit (12 Bytes) – empfohlener Standard für GCM
/// - Auth-Tag = 128 Bit (16 Bytes)
///
/// Anwendungsfälle:
/// - DEK-Wrapping (KEK = abgeleiteter Session-Key K aus HKDF)
/// - Verschlüsselung von Secrets at rest
///
/// Sicherheitshinweise:
/// - Nonces müssen pro Key unverwendet und zufällig sein (hier: RandomNumberGenerator).
/// - AAD (Additional Authenticated Data) kann Tenant-/Policy-Kontext binden (Manipulationsschutz).
/// - Bei Auth-Tag-Fehlern wirft Decrypt/DecryptAsync eine CryptographicException.
/// </summary>
public sealed class AesGcmAead : IAead
{
    /// <summary>Erwartete Nonce-Länge in Bytes (96 Bit).</summary>
    public int ExpectedNonceSizeBytes => 12;

    /// <summary>Erwartete Tag-Länge in Bytes (128 Bit).</summary>
    public int ExpectedTagSizeBytes => 16;

    public (byte[] Nonce, byte[] Tag, byte[] Cipher) Encrypt(byte[] key, byte[] plaintext, byte[]? aad = null)
    {
        ValidateKey(key);
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

        var nonce = RandomNumberGenerator.GetBytes(ExpectedNonceSizeBytes);
        var tag = new byte[ExpectedTagSizeBytes];
        var cipher = new byte[plaintext.Length];

        using var aes = new AesGcm(key, tagSizeInBytes: ExpectedTagSizeBytes);
        if (aad is { Length: > 0 })
            aes.Encrypt(nonce, plaintext, cipher, tag, aad);
        else
            aes.Encrypt(nonce, plaintext, cipher, tag);

        return (nonce, tag, cipher);
    }

    /// <summary>
    /// Verschlüsselt mit vorgegebener Nonce (z. B. für deterministische Tests).
    /// </summary>
    /// <param name="key">AES-Key (16/24/32 Bytes).</param>
    /// <param name="nonce">Nonce (IV), genau 12 Bytes.</param>
    /// <param name="plaintext">Zu verschlüsselnde Daten.</param>
    /// <param name="aad">Optional: AAD.</param>
    /// <returns>(Tag, Cipher)</returns>
    public (byte[] Tag, byte[] Cipher) Encrypt(byte[] key, byte[] nonce, byte[] plaintext, byte[]? aad = null)
    {
        ValidateKey(key);
        ValidateNonce(nonce);
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

        var tag = new byte[ExpectedTagSizeBytes];
        var cipher = new byte[plaintext.Length];

        using var aes = new AesGcm(key, tagSizeInBytes: ExpectedTagSizeBytes);
        if (aad is { Length: > 0 })
            aes.Encrypt(nonce, plaintext, cipher, tag, aad);
        else
            aes.Encrypt(nonce, plaintext, cipher, tag);

        return (tag, cipher);
    }

    /// <summary>
    /// Entschlüsselt Cipher + Tag. Wirft CryptographicException, wenn der Auth-Tag ungültig ist.
    /// </summary>
    /// <param name="key">AES-Key (16/24/32 Bytes).</param>
    /// <param name="nonce">Nonce (12 Bytes).</param>
    /// <param name="tag">Authentizitäts-Tag (16 Bytes).</param>
    /// <param name="cipher">Ciphertext.</param>
    /// <param name="aad">Optional: AAD (muss identisch zum Encrypt-Aufruf sein).</param>
    /// <returns>Plaintext.</returns>
    public byte[] Decrypt(byte[] key, byte[] nonce, byte[] tag, byte[] cipher, byte[]? aad = null)
    {
        ValidateKey(key);
        ValidateNonce(nonce);
        ValidateTag(tag);
        if (cipher is null) throw new ArgumentNullException(nameof(cipher));

        var plaintext = new byte[cipher.Length];
        using var aes = new AesGcm(key, tagSizeInBytes: ExpectedTagSizeBytes);

        if (aad is { Length: > 0 })
            aes.Decrypt(nonce, cipher, tag, plaintext, aad);
        else
            aes.Decrypt(nonce, cipher, tag, plaintext);

        return plaintext;
    }

    /// <summary>
    /// Stream-Variante von Encrypt: liest den gesamten Stream in den Speicher und verschlüsselt ihn.
    /// Für große Daten ggf. vorher in Byte-Arrays stückeln.
    /// </summary>
    public async Task<(byte[] Nonce, byte[] Tag, byte[] Cipher)> EncryptAsync(
        byte[] key,
        Stream plaintext,
        byte[]? aad = null,
        CancellationToken ct = default)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));
        using var ms = new MemoryStream();
        await plaintext.CopyToAsync(ms, ct).ConfigureAwait(false);
        return Encrypt(key, ms.ToArray(), aad);
    }

    /// <summary>
    /// Stream-Variante von Decrypt: liest den gesamten Cipher-Stream in den Speicher und entschlüsselt ihn.
    /// </summary>
    public async Task<byte[]> DecryptAsync(
        byte[] key,
        byte[] nonce,
        byte[] tag,
        Stream cipherStream,
        byte[]? aad = null,
        CancellationToken ct = default)
    {
        if (cipherStream is null) throw new ArgumentNullException(nameof(cipherStream));
        using var ms = new MemoryStream();
        await cipherStream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return Decrypt(key, nonce, tag, ms.ToArray(), aad);
    }

    /// <summary>Validiert AES-Schlüsselgröße (16/24/32 Bytes).</summary>
    private static void ValidateKey(byte[] key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (key.Length is not 16 and not 24 and not 32)
            throw new ArgumentException("AES key must be 16, 24, or 32 bytes.", nameof(key));
    }

    /// <summary>Validiert Nonce-Länge (12 Bytes).</summary>
    private void ValidateNonce(byte[] nonce)
    {
        if (nonce is null) throw new ArgumentNullException(nameof(nonce));
        if (nonce.Length != ExpectedNonceSizeBytes)
            throw new ArgumentException($"Nonce must be {ExpectedNonceSizeBytes} bytes.", nameof(nonce));
    }

    /// <summary>Validiert Tag-Länge (16 Bytes).</summary>
    private void ValidateTag(byte[] tag)
    {
        if (tag is null) throw new ArgumentNullException(nameof(tag));
        if (tag.Length != ExpectedTagSizeBytes)
            throw new ArgumentException($"Tag must be {ExpectedTagSizeBytes} bytes.", nameof(tag));
    }
}
