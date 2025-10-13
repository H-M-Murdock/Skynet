using System.Security.Cryptography;
using Skynet.Core.Crypto;

namespace Skynet.Core.Licensing;

/// <summary>
/// Clientseitiges DEK-Management:
/// - Erzeugt DEKs (kryptographisch zufällig)
/// - Wrap/Unwrap via AEAD (z. B. AES-GCM) mit optionalem AAD/Context
/// - Orchestriert ReKeying (Dual-Read/Single-Write) und liefert Migrationsstatistiken
/// </summary>
public sealed class ClientDekManager : IClientDekManager
{
    private readonly IAead _aead;

    /// <param name="aead">AEAD-Implementierung (z. B. AesGcmAead)</param>
    public ClientDekManager(IAead aead)
    {
        _aead = aead ?? throw new ArgumentNullException(nameof(aead));
    }

    /// <summary>
    /// Erzeugt kryptographisch zufällige DEK-Bytes.
    /// </summary>
    public byte[] GenerateDek(int sizeBytes = 32)
    {
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        var dek = new byte[sizeBytes];
        RandomNumberGenerator.Fill(dek);
        return dek;
    }

    /// <summary>
    /// Verpackt (wrap) einen DEK mit einem KEK (z. B. abgeleitetem Session-Key K).
    /// Resultat-Format: nonce|tag|cipher (konkateniert).
    /// </summary>
    public byte[] WrapDek(byte[] kek, byte[] dek, byte[]? aad = null)
    {
        if (kek is null || kek.Length == 0) throw new ArgumentException("KEK must not be null/empty.", nameof(kek));
        if (dek is null || dek.Length == 0) throw new ArgumentException("DEK must not be null/empty.", nameof(dek));

        var (nonce, tag, cipher) = _aead.Encrypt(kek, dek, aad);
        var wrapped = new byte[_aead.ExpectedNonceSizeBytes + _aead.ExpectedTagSizeBytes + cipher.Length];
        Buffer.BlockCopy(nonce, 0, wrapped, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, wrapped, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, wrapped, nonce.Length + tag.Length, cipher.Length);
        return wrapped;
    }

    /// <summary>
    /// Entpackt (unwrap) einen DEK aus nonce|tag|cipher.
    /// </summary>
    public byte[] UnwrapDek(byte[] kek, byte[] dekEnc, byte[]? aad = null)
    {
        if (kek is null || kek.Length == 0) throw new ArgumentException("KEK must not be null/empty.", nameof(kek));
        if (dekEnc is null || dekEnc.Length == 0) throw new ArgumentException("dekEnc must not be null/empty.", nameof(dekEnc));

        var nLen = _aead.ExpectedNonceSizeBytes;
        var tLen = _aead.ExpectedTagSizeBytes;
        if (dekEnc.Length < nLen + tLen + 1) // mindestens 1 Byte Cipher
            throw new ArgumentException("dekEnc is too short.", nameof(dekEnc));

        var nonce = new byte[nLen];
        var tag = new byte[tLen];
        var cipher = new byte[dekEnc.Length - nLen - tLen];

        Buffer.BlockCopy(dekEnc, 0, nonce, 0, nLen);
        Buffer.BlockCopy(dekEnc, nLen, tag, 0, tLen);
        Buffer.BlockCopy(dekEnc, nLen + tLen, cipher, 0, cipher.Length);

        return _aead.Decrypt(kek, nonce, tag, cipher, aad);
    }

    /// <summary>
    /// Async-Variante von Wrap (für API-Konsistenz; intern synchrone AEAD-Nutzung).
    /// </summary>
    public Task<byte[]> WrapDekAsync(byte[] kek, byte[] dek, byte[]? aad = null, CancellationToken ct = default)
        => Task.FromResult(WrapDek(kek, dek, aad));

    /// <summary>
    /// Async-Variante von Unwrap (für API-Konsistenz; intern synchrone AEAD-Nutzung).
    /// </summary>
    public Task<byte[]> UnwrapDekAsync(byte[] kek, byte[] dekEnc, byte[]? aad = null, CancellationToken ct = default)
        => Task.FromResult(UnwrapDek(kek, dekEnc, aad));

    /// <summary>
    /// ReKeying-Orchestrierung (Dual-Read/Single-Write):
    /// - Listet alle Keys (optional mit Prefix)
    /// - Liest per decryptWithOldDek den Klartext
    /// - Schreibt per encryptWithNewDek mit neuem DEK/Format
    /// Liefert RekeyResult mit Migrationsstatistik.
    /// </summary>
    public async Task<RekeyResult> RekeyAllAsync(
        ISecretStore store,
        Func<string, Task<byte[]?>> decryptWithOldDek,
        Func<string, byte[], Task> encryptWithNewDek,
        CancellationToken ct = default)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (decryptWithOldDek is null) throw new ArgumentNullException(nameof(decryptWithOldDek));
        if (encryptWithNewDek is null) throw new ArgumentNullException(nameof(encryptWithNewDek));

        var keys = await store.ListKeysAsync(prefix: null, ct).ConfigureAwait(false);
        var failedKeys = new List<string>();
        var total = 0;
        var migrated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            total++;

            try
            {
                var plain = await decryptWithOldDek(key).ConfigureAwait(false);
                if (plain is null)
                {
                    skipped++;
                    continue;
                }

                await encryptWithNewDek(key, plain).ConfigureAwait(false);
                migrated++;
            }
            catch
            {
                failed++;
                failedKeys.Add(key);
            }
        }

        return new RekeyResult(total, migrated, skipped, failed, failedKeys);
    }
}
