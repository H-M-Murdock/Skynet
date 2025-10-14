using System.Security.Cryptography;

namespace Skynet.Core.Crypto;

public sealed class EcdhKeyAgreement : IEcdhKeyAgreement
{
    public EcdhAlgorithm Algorithm { get; }

    public EcdhKeyAgreement(EcdhAlgorithm algorithm = EcdhAlgorithm.P256)
    {
        if (algorithm != EcdhAlgorithm.P256)
            throw new NotSupportedException("Nur P-256 wird unterstützt.");
        Algorithm = algorithm;
    }

    // Erwartet:
    // - privateKeyA: PKCS#8 (DER) für ECDH P-256
    // - publicKeyB: SubjectPublicKeyInfo (DER) für ECDH P-256
    public byte[] DeriveSharedSecret(byte[] privateKeyA, byte[] publicKeyB)
    {
        if (privateKeyA is null || privateKeyA.Length == 0) throw new ArgumentException("Privater Schlüssel leer.", nameof(privateKeyA));
        if (publicKeyB is null || publicKeyB.Length == 0) throw new ArgumentException("Öffentlicher Schlüssel leer.", nameof(publicKeyB));

        using var ecdhA = ECDiffieHellman.Create();
        using var ecdhB = ECDiffieHellman.Create();
        try
        {
            ecdhA.ImportPkcs8PrivateKey(privateKeyA, out _);
        }
        catch (CryptographicException ex)
        {
            throw new ArgumentException("Ungültiges PKCS#8 für ECDH P-256.", nameof(privateKeyA), ex);
        }
        try
        {
            ecdhB.ImportSubjectPublicKeyInfo(publicKeyB, out _);
        }
        catch (CryptographicException ex)
        {
            throw new ArgumentException("Ungültiges SPKI für ECDH P-256.", nameof(publicKeyB), ex);
        }

        return ecdhA.DeriveKeyMaterial(ecdhB.PublicKey);
    }

    public Task<byte[]?> DeriveSharedSecretAsync(byte[] privateKeyA, byte[] publicKeyB, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => DeriveSharedSecret(privateKeyA, publicKeyB), ct)!;
    }
}