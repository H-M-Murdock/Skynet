// Skynet.Core/Crypto/LocalAesProtector.cs

using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Crypto;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Lokaler AES-GCM-Schutz mit einem statischen App-Key (aus Config oder Fallback im Code).
/// Nicht für Hochsicherheits-anforderungen – nur Bootstrap/Core-Fallback.
/// </summary>
public sealed class InitAesProtector : ISecretProtector
{
    private readonly byte[] _key;

    // Erwartet 16/24/32 Byte Key (AES-128/192/256).
    public InitAesProtector(byte[] keyMaterial)
    {
        if (keyMaterial is null || (keyMaterial.Length != 16 && keyMaterial.Length != 24 && keyMaterial.Length != 32))
            throw new ArgumentException("Key must be 16, 24, or 32 bytes.", nameof(keyMaterial));
        _key = keyMaterial;
    }

    public string Protect(string plainText)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));

        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(12); // AES-GCM 96-bit Nonce
        var tag = new byte[16];
        var ciphertext = new byte[plaintextBytes.Length];

        using var aes = new AesGcm(_key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: [nonce|tag|cipher] → Base64
        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string cipherBase64)
    {
        if (cipherBase64 is null) throw new ArgumentNullException(nameof(cipherBase64));

        var payload = Convert.FromBase64String(cipherBase64);
        if (payload.Length < 12 + 16) throw new CryptographicException("Invalid payload length.");

        var nonce = new byte[12];
        var tag = new byte[16];
        var ciphertext = new byte[payload.Length - nonce.Length - tag.Length];

        Buffer.BlockCopy(payload, 0, nonce, 0, nonce.Length);
        Buffer.BlockCopy(payload, nonce.Length, tag, 0, tag.Length);
        Buffer.BlockCopy(payload, nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, tagSizeInBytes: 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public byte[] Protect(byte[] plain)
    {
        throw new NotImplementedException();
    }

    public byte[] Unprotect(byte[] cipher)
    {
        throw new NotImplementedException();
    }

    public Stream Protect(Stream plainStream)
    {
        throw new NotImplementedException();
    }

    public Stream Unprotect(Stream cipherStream)
    {
        throw new NotImplementedException();
    }

    public Task<string> ProtectAsync(string plainText, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> UnprotectAsync(string cipherBase64, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> ProtectAsync(byte[] plain, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> UnprotectAsync(byte[] cipher, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task ProtectAsync(Stream plainStream, Stream destination, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task UnprotectAsync(Stream cipherStream, Stream destination, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
