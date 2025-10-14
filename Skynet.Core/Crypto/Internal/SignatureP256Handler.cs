using System.Security.Cryptography;

namespace Skynet.Core.Crypto;

public sealed class SignatureP256 : ISignature
{
    public SignatureAlgorithm Algorithm => SignatureAlgorithm.ECDSA_P256;

    // Optional: frei wählbare Kennung (z. B. Thumbprint, Key-ID aus Config)
    public string? KeyId { get; }

    public SignatureP256(string? keyId = null)
    {
        KeyId = keyId;
    }

    // Erwartet:
    // - privateKey: PKCS#8 (DER) für ECDSA P-256
    // - data: beliebige Bytes (werden intern mit SHA-256 gehasht)
    // Rückgabe:
    // - DER-codierte ECDSA-Signatur (ASN.1 SEQUENCE { r, s })
    public byte[] Sign(byte[] privateKey, byte[] data)
    {
        if (privateKey is null || privateKey.Length == 0)
            throw new ArgumentException("Privater Schlüssel darf nicht leer sein.", nameof(privateKey));
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportPkcs8PrivateKey(privateKey, out _);
        }
        catch (CryptographicException ex)
        {
            throw new ArgumentException("Ungültiges P-256 PKCS#8 PrivateKey-Format.", nameof(privateKey), ex);
        }

        // SignData hasht intern mit SHA-256 und erzeugt DER-Signatur
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    // Erwartet:
    // - publicKey: SubjectPublicKeyInfo (DER) für ECDSA P-256
    // - data: Originaldaten (werden intern mit SHA-256 gehasht)
    // - signature: DER-codierte ECDSA-Signatur (ASN.1)
    public bool Verify(byte[] publicKey, byte[] data, byte[] signature)
    {
        if (publicKey is null || publicKey.Length == 0)
            throw new ArgumentException("Öffentlicher Schlüssel darf nicht leer sein.", nameof(publicKey));
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (signature is null || signature.Length == 0)
            throw new ArgumentException("Signatur darf nicht leer sein.", nameof(signature));

        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
        }
        catch (CryptographicException)
        {
            return false; // ungültiger Public-Key → Verification schlägt fehl (kein Throw)
        }

        try
        {
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            // Ungültige DER-Signaturform oder Parameter → als "false" behandeln
            return false;
        }
    }
}
