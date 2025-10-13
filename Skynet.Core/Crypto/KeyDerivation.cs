using System.Security.Cryptography;

namespace Skynet.Core.Crypto;

/// <summary>
/// HKDF-Implementierung nach RFC 5869 mit SHA-256 und SHA-512.
/// Zweck: Aus einem (nicht direkt verwendbaren) Eingabeschlüsselmaterial (IKM), z. B. ECDH-Ergebnis,
/// einen oder mehrere kryptografisch starke Schlüssel abzuleiten (KEK/SessionKey).
///
/// Begriffe:
/// - IKM (Input Keying Material): Rohmaterial (z. B. ECDH-Shared-Secret)
/// - Salt: optionales, zufälliges Salz (empfohlen; kann leer sein)
/// - Info: Kontextbindung (AAD) – z. B. "tenant/{id}/license-v1"
/// - L (lengthBytes): gewünschte Schlüssel-Länge, z. B. 32 für AES-256
///
/// Sicherheitshinweise:
/// - IKMs niemals direkt als Schlüssel verwenden; immer via HKDF ableiten.
/// - Salt sollte, wenn möglich, zufällig und nicht leer sein; bei ECDH gern aus NonceC||NonceS.
/// - Info bindet den abgeleiteten Schlüssel an einen Kontext (z. B. Tenant/Policy), um Key-Reuse zu verhindern.
/// </summary>
public sealed class KeyDerivation : IKeyDerivation
{
    /// <summary>
    /// Leitet aus IKM mittels HKDF-SHA256 einen Schlüssel der gewünschten Länge ab.
    /// </summary>
    /// <param name="ikm">Input Keying Material (z. B. ECDH-Shared-Secret); darf nicht null/leer sein.</param>
    /// <param name="salt">Optionales Salz (kann null/leer sein). Empfohlen: zufällige Bytes (z. B. NonceC||NonceS).</param>
    /// <param name="info">Optionaler Kontext (kann null/leer sein), z. B. UTF-8 "tenant/{id}/license-v1".</param>
    /// <param name="lengthBytes">Länge des abzuleitenden Schlüssels in Bytes (z. B. 32 für AES-256).</param>
    /// <returns>Abgeleiteter Schlüssel (byte[]) der Länge <paramref name="lengthBytes"/>.</returns>
    /// <exception cref="ArgumentException">Wenn ikm null/leer ist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wenn lengthBytes &lt;= 0 ist.</exception>
    public byte[] HkdfSha256(byte[] ikm, byte[]? salt, byte[]? info, int lengthBytes = 32)
        => Hkdf(ikm, salt, info, lengthBytes, useSha512: false);

    /// <summary>
    /// Leitet aus IKM mittels HKDF-SHA512 einen Schlüssel der gewünschten Länge ab.
    /// </summary>
    /// <param name="ikm">Input Keying Material (z. B. ECDH-Shared-Secret); darf nicht null/leer sein.</param>
    /// <param name="salt">Optionales Salz (kann null/leer sein). Empfohlen: zufällige Bytes.</param>
    /// <param name="info">Optionaler Kontext (kann null/leer sein), z. B. UTF-8 "tenant/{id}/license-v1".</param>
    /// <param name="lengthBytes">Länge des abzuleitenden Schlüssels in Bytes (z. B. 32/64).</param>
    /// <returns>Abgeleiteter Schlüssel (byte[]) der Länge <paramref name="lengthBytes"/>.</returns>
    /// <exception cref="ArgumentException">Wenn ikm null/leer ist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wenn lengthBytes &lt;= 0 ist.</exception>
    public byte[] HkdfSha512(byte[] ikm, byte[]? salt, byte[]? info, int lengthBytes)
        => Hkdf(ikm, salt, info, lengthBytes, useSha512: true);

    /// <summary>
    /// Gemeinsame HKDF-Logik (Extract + Expand) nach RFC 5869.
    /// </summary>
    /// <param name="ikm">Input Keying Material (nicht null/leer).</param>
    /// <param name="salt">Optionales Salz; null wird als leeres Array behandelt.</param>
    /// <param name="info">Optionale Kontextdaten; null wird als leeres Array behandelt.</param>
    /// <param name="lengthBytes">Ziel-Schlüssellänge in Bytes (&gt; 0).</param>
    /// <param name="useSha512">true = HKDF mit SHA-512, false = HKDF mit SHA-256.</param>
    /// <returns>Abgeleiteter Schlüssel der gewünschten Länge.</returns>
    private static byte[] Hkdf(byte[] ikm, byte[]? salt, byte[]? info, int lengthBytes, bool useSha512)
    {
        if (ikm is null || ikm.Length == 0) throw new ArgumentException("IKM must not be null/empty.", nameof(ikm));
        if (lengthBytes <= 0) throw new ArgumentOutOfRangeException(nameof(lengthBytes));

        // 1) Extract: PRK = HMAC(salt, IKM)
        var prk = useSha512
            ? Hmac(HashAlgorithmName.SHA512, salt ?? Array.Empty<byte>(), ikm)
            : Hmac(HashAlgorithmName.SHA256, salt ?? Array.Empty<byte>(), ikm);

        try
        {
            // 2) Expand: T(1..n) = HMAC(PRK, T(i-1) | info | counter), Key = first-L bytes of T(...)
            var infoBytes = info ?? Array.Empty<byte>();
            var result = new byte[lengthBytes];
            var hashLen = useSha512 ? 64 : 32;
            var n = (int)Math.Ceiling((double)lengthBytes / hashLen);

            var previous = Array.Empty<byte>();
            var offset = 0;

            using HMAC hmac = useSha512 ? new HMACSHA512(prk) : new HMACSHA256(prk);

            for (var i = 1; i <= n; i++)
            {
                hmac.Initialize();

                if (previous.Length > 0)
                    hmac.TransformBlock(previous, 0, previous.Length, null, 0);

                if (infoBytes.Length > 0)
                    hmac.TransformBlock(infoBytes, 0, infoBytes.Length, null, 0);

                var ctr = new[] { (byte)i };
                hmac.TransformFinalBlock(ctr, 0, ctr.Length);

                var t = hmac.Hash!;
                var toCopy = Math.Min(hashLen, lengthBytes - offset);
                Buffer.BlockCopy(t, 0, result, offset, toCopy);
                offset += toCopy;

                previous = t; // T(i)
            }

            return result;
        }
        finally
        {
            // PRK aus dem Speicher tilgen, um Key-Material nicht unnötig vorzuhalten
            CryptographicOperations.ZeroMemory(prk);
        }
    }

    /// <summary>
    /// HMAC-Hilfsfunktion (SHA-256/SHA-512).
    /// </summary>
    private static byte[] Hmac(HashAlgorithmName alg, byte[] key, byte[] data)
    {
        return alg == HashAlgorithmName.SHA512
            ? new HMACSHA512(key).ComputeHash(data)
            : new HMACSHA256(key).ComputeHash(data);
    }
}