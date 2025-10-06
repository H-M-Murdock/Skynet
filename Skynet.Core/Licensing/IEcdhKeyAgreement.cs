namespace Skynet.Core.Licensing;

public interface IEcdhKeyAgreement
{
    // Festgelegter Algorithmus (z. B. X25519 → 32-Byte Keys)
    EcdhAlgorithm Algorithm { get; }

    // ECDH(PrivA, PubB) → IKM (Rohmaterial für HKDF). Wirft ArgumentException bei ungültigen Längen/Formaten.
    byte[] DeriveSharedSecret(byte[] privateKeyA, byte[] publicKeyB);

    // Optional asynchron (HSM/KMS-Fälle)
    Task<byte[]> DeriveSharedSecretAsync(byte[] privateKeyA, byte[] publicKeyB, CancellationToken ct = default);
}