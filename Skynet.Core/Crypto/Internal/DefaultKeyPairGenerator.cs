using System.Security.Cryptography;

namespace Skynet.Core.Crypto;

public sealed class DefaultKeyPairGenerator : IKeyPairGenerator
{
    public EcdhAlgorithm EcdhAlg { get; }
    public SignatureAlgorithm SignAlg { get; }

    private const int X25519KeySize = 32;

    public DefaultKeyPairGenerator(
        EcdhAlgorithm ecdhAlg = EcdhAlgorithm.X25519,
        SignatureAlgorithm signAlg = SignatureAlgorithm.ECDSA_P256)
    {
        if (ecdhAlg != EcdhAlgorithm.X25519)
            throw new NotSupportedException($"ECDH-Algorithmus {ecdhAlg} wird nicht unterstützt.");
        if (signAlg != SignatureAlgorithm.ECDSA_P256)
            throw new NotSupportedException($"Signatur-Algorithmus {signAlg} wird nicht unterstützt.");

        EcdhAlg = ecdhAlg;
        SignAlg = signAlg;
    }

    public (byte[] PrivateKey, byte[] PublicKey) GenerateEcdhKeyPair()
    {
        EnsureEcdhSupported();

        var sk = RandomNumberGenerator.GetBytes(X25519KeySize);
        var pk = GetX25519PublicKey(sk);
        return (sk, pk);
    }

    public (byte[] PrivateKey, byte[] PublicKey) GenerateEcdhKeyPair(ReadOnlySpan<byte> seed)
    {
        EnsureEcdhSupported();

        var sk = Derive32(seed, "Skynet.Core.Crypto:ECDH:X25519");
        var pk = GetX25519PublicKey(sk);
        return (sk, pk);
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

        // deterministisch: 32-Byte Secret via HKDF-SHA512 ableiten und als D importieren
        var sk = Derive32(seed, "Skynet.Core.Crypto:SIGN:ECDSA-P256");

        using var ecdsa = ECDsa.Create();
        var curve = ECCurve.NamedCurves.nistP256;

        // D muss 32 Byte sein. Wir verwenden sk direkt; ImportParameters reduziert ggf. intern modulo n.
        var parameters = new ECParameters { Curve = curve, D = sk };
        ecdsa.ImportParameters(parameters);

        var priv = ecdsa.ExportPkcs8PrivateKey();
        var pub = ecdsa.ExportSubjectPublicKeyInfo();
        return (priv, pub);
    }

    private static byte[] GetX25519PublicKey(ReadOnlySpan<byte> privateKey)
    {
        if (privateKey.Length != X25519KeySize)
            throw new ArgumentException($"X25519 Private Key muss {X25519KeySize} Bytes haben.", nameof(privateKey));

        using var ecdh = ECDiffieHellman.Create();
        ecdh.GenerateKey(ECCurve.CreateFromFriendlyName("X25519"));

        var parms = new ECParameters
        {
            Curve = ECCurve.CreateFromFriendlyName("X25519"),
            D = privateKey.ToArray()
        };
        ecdh.ImportParameters(parms);

        var pub = ecdh.ExportParameters(includePrivateParameters: false).Q.X;
        if (pub is null || pub.Length != X25519KeySize)
            throw new ArgumentException("X25519 Public Key konnte nicht erzeugt werden.");

        return pub;
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
        if (EcdhAlg != EcdhAlgorithm.X25519)
            throw new NotSupportedException("Nur X25519 wird unterstützt.");
    }

    private void EnsureSignSupported()
    {
        if (SignAlg != SignatureAlgorithm.ECDSA_P256)
            throw new NotSupportedException("Nur ECDSA P-256 wird unterstützt.");
    }
}