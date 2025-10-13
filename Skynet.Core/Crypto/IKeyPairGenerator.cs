namespace Skynet.Core.Crypto;

public interface IKeyPairGenerator
{
    // Konfigurierte Algorithmen, damit Call-Sites Keylängen/Encoding kennen.
    EcdhAlgorithm EcdhAlg { get; }
    SignatureAlgorithm SignAlg { get; }

    // ECDH (z. B. X25519 → 32-Byte Keys). Wirft bei Fehlern ArgumentException.
    (byte[] PrivateKey, byte[] PublicKey) GenerateEcdhKeyPair();

    // Optional deterministisch (Seed-basiert), nur für Tests/Recovery.
    (byte[] PrivateKey, byte[] PublicKey) GenerateEcdhKeyPair(ReadOnlySpan<byte> seed);

    // Signatur-Schlüsselpaar (z. B. Ed25519 → 32-Byte Seed, 32-Byte Pub).
    (byte[] PrivateKey, byte[] PublicKey) GenerateSignKeyPair();

    // Optional deterministisch (Seed-basiert)
    (byte[] PrivateKey, byte[] PublicKey) GenerateSignKeyPair(ReadOnlySpan<byte> seed);
}