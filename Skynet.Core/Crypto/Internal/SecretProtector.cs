using System.Buffers;
using System.Security.Cryptography;
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

    // String (UTF-8) -> Base64 (ohne AAD)
    public string Protect(string plainText)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));
        byte[]? bytes = null;
        try
        {
            bytes = Encoding.UTF8.GetBytes(plainText);
            var packed = Protect(bytes);
            return Convert.ToBase64String(packed);
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public string Unprotect(string cipherBase64)
    {
        if (cipherBase64 is null) throw new ArgumentNullException(nameof(cipherBase64));
        byte[]? packed = null;
        byte[]? bytes = null;
        try
        {
            packed = Convert.FromBase64String(cipherBase64);
            bytes = UnpackAndDecrypt(packed);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            if (packed is not null) CryptographicOperations.ZeroMemory(packed);
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    // AAD-fähige Overloads (String AAD)
    public string Protect(string plainText, string? aad)
        => Protect(plainText, aad is null ? null : Encoding.UTF8.GetBytes(aad));

    public string Unprotect(string cipherBase64, string? aad)
        => Unprotect(cipherBase64, aad is null ? null : Encoding.UTF8.GetBytes(aad));

    // AAD-fähige Overloads (Bytes AAD)
    public string Protect(string plainText, byte[]? aad)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));
        byte[]? bytes = null;
        try
        {
            bytes = Encoding.UTF8.GetBytes(plainText);
            var packed = Protect(bytes, aad);
            return Convert.ToBase64String(packed);
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public string Unprotect(string cipherBase64, byte[]? aad)
    {
        if (cipherBase64 is null) throw new ArgumentNullException(nameof(cipherBase64));
        byte[]? packed = null;
        byte[]? bytes = null;
        try
        {
            packed = Convert.FromBase64String(cipherBase64);
            bytes = UnpackAndDecrypt(packed, aad);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            if (packed is not null) CryptographicOperations.ZeroMemory(packed);
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    // Bytes -> Bytes (verpacktes Format: [nonce|tag|cipher]) ohne AAD
    public byte[] Protect(byte[] plain)
    {
        if (plain is null) throw new ArgumentNullException(nameof(plain));

        var (nonce, tag, cipher) = _aead.Encrypt(_key, plain);

        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);

        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(tag);
        CryptographicOperations.ZeroMemory(cipher);

        return result;
    }

    public byte[] Unprotect(byte[] cipher)
    {
        if (cipher is null) throw new ArgumentNullException(nameof(cipher));
        return UnpackAndDecrypt(cipher);
    }

    // Bytes -> Bytes (verpacktes Format: [nonce|tag|cipher]) mit AAD
    public byte[] Protect(byte[] plain, byte[]? aad)
    {
        if (plain is null) throw new ArgumentNullException(nameof(plain));

        var (nonce, tag, cipher) = _aead.Encrypt(_key, plain, aad);

        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);

        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(tag);
        CryptographicOperations.ZeroMemory(cipher);

        return result;
    }

    public byte[] Unprotect(byte[] cipher, byte[]? aad)
    {
        if (cipher is null) throw new ArgumentNullException(nameof(cipher));
        return UnpackAndDecrypt(cipher, aad);
    }

    // Stream -> Stream (ohne AAD) – Rückgabe-Stream Position = 0
    public Stream Protect(Stream plainStream)
    {
        if (plainStream is null) throw new ArgumentNullException(nameof(plainStream));
        using var ms = new MemoryStream();
        plainStream.CopyTo(ms);
        var plainBytes = ms.ToArray();
        try
        {
            var packed = Protect(plainBytes);
            return new MemoryStream(packed, writable: false) { Position = 0 };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public Stream Unprotect(Stream cipherStream)
    {
        if (cipherStream is null) throw new ArgumentNullException(nameof(cipherStream));
        using var ms = new MemoryStream();
        cipherStream.CopyTo(ms);
        var packed = ms.ToArray();
        
        // BUGFIX: Wir dürfen 'plain' hier NICHT wipen, da der zurückgegebene MemoryStream
        // direkt auf diesem Array arbeitet (by reference).
        byte[] plain;
        try
        {
            plain = UnpackAndDecrypt(packed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(packed);
        }
        
        return new MemoryStream(plain, writable: false) { Position = 0 };
    }

    // Stream -> Stream (mit AAD) – Rückgabe-Stream Position = 0
    public Stream Protect(Stream plainStream, byte[]? aad)
    {
        if (plainStream is null) throw new ArgumentNullException(nameof(plainStream));
        using var ms = new MemoryStream();
        plainStream.CopyTo(ms);
        var plainBytes = ms.ToArray();
        try
        {
            var packed = Protect(plainBytes, aad);
            return new MemoryStream(packed, writable: false) { Position = 0 };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public Stream Unprotect(Stream cipherStream, byte[]? aad)
    {
        if (cipherStream is null) throw new ArgumentNullException(nameof(cipherStream));
        using var ms = new MemoryStream();
        cipherStream.CopyTo(ms);
        var packed = ms.ToArray();
        
        // BUGFIX: plain nicht wipen
        byte[] plain;
        try
        {
            plain = UnpackAndDecrypt(packed, aad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(packed);
        }
        
        return new MemoryStream(plain, writable: false) { Position = 0 };
    }

    // Async (ohne AAD)
    public async Task<string> ProtectAsync(string plainText, CancellationToken ct = default)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));
        ct.ThrowIfCancellationRequested();

        byte[]? bytes = null;
        try
        {
            bytes = Encoding.UTF8.GetBytes(plainText);
            var packed = await ProtectAsync(bytes, ct).ConfigureAwait(false);
            return Convert.ToBase64String(packed);
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<string> UnprotectAsync(string cipherBase64, CancellationToken ct = default)
    {
        if (cipherBase64 is null) throw new ArgumentNullException(nameof(cipherBase64));
        ct.ThrowIfCancellationRequested();

        byte[]? packed = null;
        byte[]? plain = null;
        try
        {
            packed = Convert.FromBase64String(cipherBase64);
            plain = await UnprotectAsync(packed, ct).ConfigureAwait(false);
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            if (packed is not null) CryptographicOperations.ZeroMemory(packed);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
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

        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(tag);
        CryptographicOperations.ZeroMemory(cipher);

        return result;
    }

    public async Task<byte[]> UnprotectAsync(byte[] cipher, CancellationToken ct = default)
    {
        if (cipher is null) throw new ArgumentNullException(nameof(cipher));
        ct.ThrowIfCancellationRequested();

        var (nonce, tag, enc) = SplitPacked(cipher);
        using var encStream = new MemoryStream(enc, writable: false);
        try
        {
            return await _aead.DecryptAsync(_key, nonce, tag, encStream, null, ct).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(enc);
        }
    }

    // Async (mit AAD)
    public async Task<string> ProtectAsync(string plainText, byte[]? aad, CancellationToken ct = default)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));
        ct.ThrowIfCancellationRequested();

        byte[]? bytes = null;
        try
        {
            bytes = Encoding.UTF8.GetBytes(plainText);
            var packed = await ProtectAsync(bytes, aad, ct).ConfigureAwait(false);
            return Convert.ToBase64String(packed);
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<string> UnprotectAsync(string cipherBase64, byte[]? aad, CancellationToken ct = default)
    {
        if (cipherBase64 is null) throw new ArgumentNullException(nameof(cipherBase64));
        ct.ThrowIfCancellationRequested();

        byte[]? packed = null;
        byte[]? plain = null;
        try
        {
            packed = Convert.FromBase64String(cipherBase64);
            plain = await UnprotectAsync(packed, aad, ct).ConfigureAwait(false);
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            if (packed is not null) CryptographicOperations.ZeroMemory(packed);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
    }

    public async Task<byte[]> ProtectAsync(byte[] plain, byte[]? aad, CancellationToken ct = default)
    {
        if (plain is null) throw new ArgumentNullException(nameof(plain));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream(plain, writable: false);
        var (nonce, tag, cipher) = await _aead.EncryptAsync(_key, ms, aad, ct).ConfigureAwait(false);

        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, result, nonce.Length + tag.Length, cipher.Length);

        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(tag);
        CryptographicOperations.ZeroMemory(cipher);

        return result;
    }

    public async Task<byte[]> UnprotectAsync(byte[] cipher, byte[]? aad, CancellationToken ct = default)
    {
        if (cipher is null) throw new ArgumentNullException(nameof(cipher));
        ct.ThrowIfCancellationRequested();

        var (nonce, tag, enc) = SplitPacked(cipher);
        using var encStream = new MemoryStream(enc, writable: false);
        try
        {
            return await _aead.DecryptAsync(_key, nonce, tag, encStream, aad, ct).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(enc);
        }
    }

    public async Task ProtectAsync(Stream plainStream, Stream destination, CancellationToken ct = default)
    {
        if (plainStream is null) throw new ArgumentNullException(nameof(plainStream));
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        await plainStream.CopyToAsync(ms, ct).ConfigureAwait(false);

        var plainBytes = ms.ToArray();
        try
        {
            var packed = await ProtectAsync(plainBytes, ct).ConfigureAwait(false);
            await destination.WriteAsync(packed, ct).ConfigureAwait(false);
            if (destination.CanSeek) destination.Position = 0;
            CryptographicOperations.ZeroMemory(packed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public async Task UnprotectAsync(Stream cipherStream, Stream destination, CancellationToken ct = default)
    {
        if (cipherStream is null) throw new ArgumentNullException(nameof(cipherStream));
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        await cipherStream.CopyToAsync(ms, ct).ConfigureAwait(false);

        var packed = ms.ToArray();
        byte[]? plain = null;
        try
        {
            plain = await UnprotectAsync(packed, ct).ConfigureAwait(false);
            await destination.WriteAsync(plain, ct).ConfigureAwait(false);
            if (destination.CanSeek) destination.Position = 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(packed);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
    }

    // Async Stream (mit AAD)
    public async Task ProtectAsync(Stream plainStream, Stream destination, byte[]? aad, CancellationToken ct = default)
    {
        if (plainStream is null) throw new ArgumentNullException(nameof(plainStream));
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        await plainStream.CopyToAsync(ms, ct).ConfigureAwait(false);

        var plainBytes = ms.ToArray();
        try
        {
            var packed = await ProtectAsync(plainBytes, aad, ct).ConfigureAwait(false);
            await destination.WriteAsync(packed, ct).ConfigureAwait(false);
            if (destination.CanSeek) destination.Position = 0;
            CryptographicOperations.ZeroMemory(packed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public async Task UnprotectAsync(Stream cipherStream, Stream destination, byte[]? aad, CancellationToken ct = default)
    {
        if (cipherStream is null) throw new ArgumentNullException(nameof(cipherStream));
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        await cipherStream.CopyToAsync(ms, ct).ConfigureAwait(false);

        var packed = ms.ToArray();
        byte[]? plain = null;
        try
        {
            plain = await UnprotectAsync(packed, aad, ct).ConfigureAwait(false);
            await destination.WriteAsync(plain, ct).ConfigureAwait(false);
            if (destination.CanSeek) destination.Position = 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(packed);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
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
        try
        {
            return _aead.Decrypt(_key, nonce, tag, enc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(enc);
        }
    }

    private byte[] UnpackAndDecrypt(byte[] packed, byte[]? aad)
    {
        var (nonce, tag, enc) = SplitPacked(packed);
        try
        {
            return _aead.Decrypt(_key, nonce, tag, enc, aad);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(enc);
        }
    }
}