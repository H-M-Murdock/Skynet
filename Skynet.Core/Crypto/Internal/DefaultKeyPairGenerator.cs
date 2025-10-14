using System.Security.Cryptography;

namespace Skynet.Core.Crypto;

public sealed class DefaultKeyPairGenerator : IKeyPairGenerator
{
    public EcdhAlgorithm EcdhAlg { get; }
    public SignatureAlgorithm SignAlg { get; }

    public DefaultKeyPairGenerator(
        EcdhAlgorithm ecdhAlg = EcdhAlgorithm.P256,
        SignatureAlgorithm signAlg = SignatureAlgorithm.ECDSA_P256)
    {
        if (ecdhAlg != EcdhAlgorithm.P256)
            throw new NotSupportedException($"ECDH-Algorithmus {ecdhAlg} wird nicht unterstützt.");
        if (signAlg != SignatureAlgorithm.ECDSA_P256)
            throw new NotSupportedException($"Signatur-Algorithmus {signAlg} wird nicht unterstützt.");

        EcdhAlg = ecdhAlg;
        SignAlg = signAlg;
    }

    public (byte[] PrivateKey, byte[] PublicKey) GenerateEcdhKeyPair()
    {
        EnsureEcdhSupported();

        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var priv = ecdh.ExportPkcs8PrivateKey();            // PKCS#8 (DER)
        var pub = ecdh.ExportSubjectPublicKeyInfo();        // SPKI (DER)
        return (priv, pub);
    }

    public (byte[] PrivateKey, byte[] PublicKey) GenerateEcdhKeyPair(ReadOnlySpan<byte> seed)
    {
        EnsureEcdhSupported();

        // deterministisch: 32-Byte Secret via HKDF-SHA512 ableiten und als D importieren
        var d = Derive32(seed, "Skynet.Core.Crypto:ECDH:P256");

        using var ecdh = ECDiffieHellman.Create();
        var curve = ECCurve.NamedCurves.nistP256;
        ecdh.ImportParameters(new ECParameters { Curve = curve, D = d });

        var priv = ecdh.ExportPkcs8PrivateKey();
        var pub = ecdh.ExportSubjectPublicKeyInfo();
        return (priv, pub);
    }

    // ECDSA P-256: Export als PKCS#8 (privat) und SPKI (öffentlich), beides DER-codiert.
    public (byte[] PrivateKey, byte[] PublicKey) GenerateSignKeyPair()
    {
        EnsureSignSupported();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var priv = ecdsa.ExportPkcs8PrivateKey();
        var pub = ecdsa.ExportSubjectPublicKeyInfo();
        return (priv, pub);
    }

    public (byte[] PrivateKey, byte[] PublicKey) GenerateSignKeyPair(ReadOnlySpan<byte> seed)
    {
        EnsureSignSupported();

        var sk = Derive32(seed, "Skynet.Core.Crypto:SIGN:ECDSA-P256");

        using var ecdsa = ECDsa.Create();
        var curve = ECCurve.NamedCurves.nistP256;
        ecdsa.ImportParameters(new ECParameters { Curve = curve, D = sk });

        var priv = ecdsa.ExportPkcs8PrivateKey();
        var pub = ecdsa.ExportSubjectPublicKeyInfo();
        return (priv, pub);
    }

    private static byte[] Derive32(ReadOnlySpan<byte> seed, string context)
    {
        if (seed.Length == 0)
            throw new ArgumentException("Seed darf nicht leer sein.", nameof(seed));

        var kd = new KeyDerivation();
        var info = System.Text.Encoding.UTF8.GetBytes(context);
        return kd.HkdfSha512(seed.ToArray(), salt: [], info: info, lengthBytes: 32);
    }

    private void EnsureEcdhSupported()
    {
        if (EcdhAlg != EcdhAlgorithm.P256)
            throw new NotSupportedException("Nur P-256 wird unterstützt.");
    }

    private void EnsureSignSupported()
    {
        if (SignAlg != SignatureAlgorithm.ECDSA_P256)
            throw new NotSupportedException("Nur ECDSA P-256 wird unterstützt.");
    }
}