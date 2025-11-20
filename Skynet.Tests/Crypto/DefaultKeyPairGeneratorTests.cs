using System.Security.Cryptography;
using Skynet.Core.Crypto;
using Xunit;

namespace Skynet.Tests.Crypto;

public class DefaultKeyPairGeneratorTests
{
    private readonly DefaultKeyPairGenerator _sut = new();

    [Fact]
    public void Constructor_SetsCorrectAlgorithms()
    {
        Assert.Equal(EcdhAlgorithm.P256, _sut.EcdhAlg);
        Assert.Equal(SignatureAlgorithm.ECDSA_P256, _sut.SignAlg);
    }

    [Fact]
    public void Constructor_ThrowsOnUnsupportedAlgorithms()
    {
        // Wir casten hier auf ungültige Enum-Werte oder andere Algorithmen (falls das Enum erweitert wird)
        // Aktuell gibt es nur P256, also testen wir hypothetische ungültige Werte
        Assert.Throws<NotSupportedException>(() => 
            new DefaultKeyPairGenerator((EcdhAlgorithm)999, SignatureAlgorithm.ECDSA_P256));
            
        Assert.Throws<NotSupportedException>(() => 
            new DefaultKeyPairGenerator(EcdhAlgorithm.P256, (SignatureAlgorithm)999));
    }

    [Fact]
    public void GenerateEcdhKeyPair_ReturnsValidKeys()
    {
        // Act
        var (priv, pub) = _sut.GenerateEcdhKeyPair();

        // Assert
        Assert.NotNull(priv);
        Assert.NotNull(pub);
        Assert.True(priv.Length > 0);
        Assert.True(pub.Length > 0);

        // Validierung: Können wir die Keys in .NET importieren?
        using var ecdh = ECDiffieHellman.Create();
        ecdh.ImportPkcs8PrivateKey(priv, out _); // Muss erfolgreich sein
        ecdh.ImportSubjectPublicKeyInfo(pub, out _); // Muss erfolgreich sein
    }

    [Fact]
    public void GenerateEcdhKeyPair_Deterministic_SameSeedProducesSameKeys()
    {
        // Arrange
        var seed = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3, 4, 5, 6 }; // Beliebiges Byte-Array

        // Act
        var pair1 = _sut.GenerateEcdhKeyPair(seed);
        var pair2 = _sut.GenerateEcdhKeyPair(seed);

        // Assert
        Assert.Equal(pair1.PrivateKey, pair2.PrivateKey);
        Assert.Equal(pair1.PublicKey, pair2.PublicKey);
        
        // Gegentest: Anderer Seed muss andere Keys liefern
        var seedB = new byte[] { 0xFF, 0xFF, 0xFF };
        var pair3 = _sut.GenerateEcdhKeyPair(seedB);
        Assert.NotEqual(pair1.PrivateKey, pair3.PrivateKey);
    }

    [Fact]
    public void GenerateEcdhKeyPair_Deterministic_EmptySeedThrowsException()
    {
        Assert.Throws<ArgumentException>(() => _sut.GenerateEcdhKeyPair(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void GenerateSignKeyPair_ReturnsValidKeys()
    {
        // Act
        var (priv, pub) = _sut.GenerateSignKeyPair();

        // Assert
        Assert.NotNull(priv);
        Assert.NotNull(pub);

        // Validierung: Import in ECDsa
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(priv, out _);
        ecdsa.ImportSubjectPublicKeyInfo(pub, out _);
    }

    [Fact]
    public void GenerateSignKeyPair_Deterministic_SameSeedProducesSameKeys()
    {
        // Arrange
        var seed = RandomNumberGenerator.GetBytes(32);

        // Act
        var pair1 = _sut.GenerateSignKeyPair(seed);
        var pair2 = _sut.GenerateSignKeyPair(seed);

        // Assert
        Assert.Equal(pair1.PrivateKey, pair2.PrivateKey);
        Assert.Equal(pair1.PublicKey, pair2.PublicKey);
    }

    [Fact]
    public void GenerateSignKeyPair_Deterministic_EmptySeedThrowsException()
    {
        Assert.Throws<ArgumentException>(() => _sut.GenerateSignKeyPair(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void GeneratedKeys_AreInteroperable()
    {
        // Testet, ob der Private Key zum Public Key passt (durch Signieren/Verifizieren)
        var (priv, pub) = _sut.GenerateSignKeyPair();
        var data = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };

        // Signer
        using var signer = ECDsa.Create();
        signer.ImportPkcs8PrivateKey(priv, out _);
        var signature = signer.SignData(data, HashAlgorithmName.SHA256);

        // Verifier
        using var verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(pub, out _);
        var valid = verifier.VerifyData(data, signature, HashAlgorithmName.SHA256);

        Assert.True(valid, "Der generierte Public Key muss Signaturen des Private Keys verifizieren können.");
    }
}
