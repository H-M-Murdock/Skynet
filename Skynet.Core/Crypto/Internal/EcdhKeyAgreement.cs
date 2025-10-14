using System.Security.Cryptography;

namespace Skynet.Core.Crypto;

public sealed class EcdhKeyAgreement : IEcdhKeyAgreement
{
    public EcdhAlgorithm Algorithm { get; }

    private const int X25519KeySize = 32;

    public EcdhKeyAgreement(EcdhAlgorithm algorithm = EcdhAlgorithm.X25519)
    {
        if (algorithm != EcdhAlgorithm.X25519)
            throw new NotSupportedException("Nur X25519 wird unterstützt.");
        Algorithm = algorithm;
    }

    public byte[] DeriveSharedSecret(byte[] privateKeyA, byte[] publicKeyB)
    {
        if (privateKeyA is null) throw new ArgumentNullException(nameof(privateKeyA));
        if (publicKeyB is null) throw new ArgumentNullException(nameof(publicKeyB));
        EnsureX25519Lengths(privateKeyA, publicKeyB);

        var curve = ECCurve.CreateFromFriendlyName("X25519");

        using var ecdhA = ECDiffieHellman.Create();
        ecdhA.ImportParameters(new ECParameters { Curve = curve, D = privateKeyA });

        using var ecdhBpub = ECDiffieHellman.Create();
        ecdhBpub.ImportParameters(new ECParameters { Curve = curve, Q = new ECPoint { X = publicKeyB } });

        // Rohes ECDH-Ergebnis (IKM) zurückgeben – danach via HKDF ableiten.
        return ecdhA.DeriveKeyMaterial(ecdhBpub.PublicKey);
    }

    public Task<byte[]> DeriveSharedSecretAsync(byte[] privateKeyA, byte[] publicKeyB, CancellationToken ct = default)
    {
        // CPU-bound → einfache Task.Run-Kapselung, Cancel unterbricht nicht die native Crypto,
        // aber respektiert Abbruch vor Start.
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => DeriveSharedSecret(privateKeyA, publicKeyB), ct);
    }

    private static void EnsureX25519Lengths(byte[] priv, byte[] pub)
    {
        if (priv.Length != X25519KeySize)
            throw new ArgumentException($"X25519 privater Schlüssel muss {X25519KeySize} Bytes haben.", nameof(priv));
        if (pub.Length != X25519KeySize)
            throw new ArgumentException($"X25519 öffentlicher Schlüssel muss {X25519KeySize} Bytes haben.", nameof(pub));
    }
}
