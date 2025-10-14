using System.Text;

namespace Skynet.Core.Crypto;

public sealed class SecretProtector : ISecretProtector
{
    private readonly IAead _aead;
    private readonly byte[] _key;

    // Erwartet symmetrischen Schlüssel (z. B. 32-Byte AES-256). IAead validiert die Größe.
    public SecretProtector(IAead aead, byte[] key)
    {
        _aead = aead ?? throw new ArgumentNullException(nameof(aead));
        _key = key ?? throw new ArgumentNullException(nameof(key));
    }

    // String (UTF-8) -> Base64
    public string Protect(string plainText)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var packed = Protect(bytes);
        return Convert.ToBase64String(packed);
    }

    public string Unprotect(string cipherBase64)
    {
        if (cipherBase64 is null) throw new ArgumentNullException(nameof(cipherBase64));
        var packed = Convert.FromBase64String(cipherBase64);
        var bytes = UnpackAndDecrypt(packed);
        return Encoding.UTF8.GetString(bytes);
    }

    // Bytes -> Bytes (verpacktes Format: [nonce|tag|cipher])
    public byte[] Protect(byte[] plain)
    {
        if (plain is null) throw new ArgumentNullException(nameof(plain));

        var (nonce, tag, cipher) = _aead.Encrypt(_key, plain);

        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);
        return result;
    }

    public byte[] Unprotect(byte[] cipher)
    {
        if (cipher is null) throw new ArgumentNullException(nameof(cipher));
        return UnpackAndDecrypt(cipher);
    }

    // Stream -> Stream
    public Stream Protect(Stream plainStream)
    {
        if (plainStream is null) throw new ArgumentNullException(nameof(plainStream));
        using var ms = new MemoryStream();
        plainStream.CopyTo(ms);
        var packed = Protect(ms.ToArray());
        return new MemoryStream(packed, writable: false) { Position = packed.Length };
    }

    public Stream Unprotect(Stream cipherStream)
    {
        if (cipherStream is null) throw new ArgumentNullException(nameof(cipherStream));
        using var ms = new MemoryStream();
        cipherStream.CopyTo(ms);
        var plain = UnpackAndDecrypt(ms.ToArray());
        return new MemoryStream(plain, writable: false) { Position = plain.Length };
    }

    // Async
    public async Task<string> ProtectAsync(string plainText, CancellationToken ct = default)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));
        ct.ThrowIfCancellationRequested();
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var packed = await ProtectAsync(bytes, ct).ConfigureAwait(false);
        return Convert.ToBase64String(packed);
    }

    public async Task<string> UnprotectAsync(string cipherBase64, CancellationToken ct = default)
    {
        if (cipherBase64 is null) throw new ArgumentNullException(nameof(cipherBase64));
        ct.ThrowIfCancellationRequested();
        var packed = Convert.FromBase64String(cipherBase64);
        var plain = await UnprotectAsync(packed, ct).ConfigureAwait(false);
        return Encoding.UTF8.GetString(plain);
    }

    public async Task<byte[]> ProtectAsync(byte[] plain, CancellationToken ct = default)
    {
        if (plain is null) throw new ArgumentNullException(nameof(plain));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream(plain, writable: false);
        var (nonce, tag, cipher) = await _aead.EncryptAsync(_key, ms, null, ct).ConfigureAwait(false);

        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);
        return result;
    }

    public async Task<byte[]> UnprotectAsync(byte[] cipher, CancellationToken ct = default)
    {
        if (cipher is null) throw new ArgumentNullException(nameof(cipher));
        ct.ThrowIfCancellationRequested();

        var (nonce, tag, enc) = SplitPacked(cipher);
        using var encStream = new MemoryStream(enc, writable: false);
        return await _aead.DecryptAsync(_key, nonce, tag, encStream, null, ct).ConfigureAwait(false);
    }

    public async Task ProtectAsync(Stream plainStream, Stream destination, CancellationToken ct = default)
    {
        if (plainStream is null) throw new ArgumentNullException(nameof(plainStream));
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        await plainStream.CopyToAsync(ms, ct).ConfigureAwait(false);

        var packed = await ProtectAsync(ms.ToArray(), ct).ConfigureAwait(false);
        await destination.WriteAsync(packed, ct).ConfigureAwait(false);
    }

    public async Task UnprotectAsync(Stream cipherStream, Stream destination, CancellationToken ct = default)
    {
        if (cipherStream is null) throw new ArgumentNullException(nameof(cipherStream));
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        await cipherStream.CopyToAsync(ms, ct).ConfigureAwait(false);

        var plain = await UnprotectAsync(ms.ToArray(), ct).ConfigureAwait(false);
        await destination.WriteAsync(plain, ct).ConfigureAwait(false);
    }

    // Helpers

    private (byte[] Nonce, byte[] Tag, byte[] Enc) SplitPacked(byte[] packed)
    {
        var n = _aead.ExpectedNonceSizeBytes;
        var t = _aead.ExpectedTagSizeBytes;

        if (packed.Length < n + t)
            throw new ArgumentException("Cipher ist zu kurz.", nameof(packed));

        var nonce = new byte[n];
        var tag = new byte[t];
        var enc = new byte[packed.Length - n - t];

        Buffer.BlockCopy(packed, 0, nonce, 0, n);
        Buffer.BlockCopy(packed, n, tag, 0, t);
        Buffer.BlockCopy(packed, n + t, enc, 0, enc.Length);
        return (nonce, tag, enc);
    }

    private byte[] UnpackAndDecrypt(byte[] packed)
    {
        var (nonce, tag, enc) = SplitPacked(packed);
        return _aead.Decrypt(_key, nonce, tag, enc);
    }
}
