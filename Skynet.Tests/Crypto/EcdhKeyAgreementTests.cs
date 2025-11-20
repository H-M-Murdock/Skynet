using System.Security.Cryptography;
using Skynet.Core.Crypto;
using Xunit;

namespace Skynet.Tests.Crypto;

public class EcdhKeyAgreementTests
{
    private readonly EcdhKeyAgreement _sut = new();
    private readonly DefaultKeyPairGenerator _gen = new();

    [Fact]
    public void Constructor_ThrowsOnUnsupportedAlgorithm()
    {
        Assert.Throws<NotSupportedException>(() => new EcdhKeyAgreement((EcdhAlgorithm)999));
    }

    [Fact]
    public void DeriveSharedSecret_AgreementIsSymmetric()
    {
        // Arrange: Erzeuge zwei Parteien Alice und Bob
        var (privA, pubA) = _gen.GenerateEcdhKeyPair();
        var (privB, pubB) = _gen.GenerateEcdhKeyPair();

        // Act: Alice berechnet Secret mit Bobs Public Key
        var secretA = _sut.DeriveSharedSecret(privA, pubB);

        // Act: Bob berechnet Secret mit Alices Public Key
        var secretB = _sut.DeriveSharedSecret(privB, pubA);

        // Assert: Beide müssen zum gleichen Ergebnis kommen
        Assert.NotNull(secretA);
        Assert.NotNull(secretB);
        Assert.NotEmpty(secretA);
        Assert.Equal(secretA, secretB);
    }
    
    [Fact]
    public async Task DeriveSharedSecretAsync_AgreementIsSymmetric()
    {
        // Arrange
        var (privA, pubA) = _gen.GenerateEcdhKeyPair();
        var (privB, pubB) = _gen.GenerateEcdhKeyPair();

        // Act
        var secretA = await _sut.DeriveSharedSecretAsync(privA, pubB);
        var secretB = await _sut.DeriveSharedSecretAsync(privB, pubA);

        // Assert
        Assert.NotNull(secretA);
        Assert.Equal(secretA, secretB);
    }

    [Fact]
    public void DeriveSharedSecret_WithDifferentKeys_ProducesDifferentSecrets()
    {
        // Arrange
        var (privA, pubA) = _gen.GenerateEcdhKeyPair();
        var (privB, pubB) = _gen.GenerateEcdhKeyPair();
        var (privC, pubC) = _gen.GenerateEcdhKeyPair(); // Dritte Partei

        // Act
        var secretAB = _sut.DeriveSharedSecret(privA, pubB);
        var secretAC = _sut.DeriveSharedSecret(privA, pubC);

        // Assert
        Assert.NotEqual(secretAB, secretAC);
    }

    [Fact]
    public void DeriveSharedSecret_NullOrEmptyKeys_ThrowsArgumentException()
    {
        var validKey = _gen.GenerateEcdhKeyPair().PrivateKey; // Dummy
        
        Assert.Throws<ArgumentException>(() => _sut.DeriveSharedSecret(null!, validKey));
        Assert.Throws<ArgumentException>(() => _sut.DeriveSharedSecret(Array.Empty<byte>(), validKey));
        
        Assert.Throws<ArgumentException>(() => _sut.DeriveSharedSecret(validKey, null!));
        Assert.Throws<ArgumentException>(() => _sut.DeriveSharedSecret(validKey, Array.Empty<byte>()));
    }

    [Fact]
    public void DeriveSharedSecret_InvalidPrivateKeyFormat_ThrowsArgumentException()
    {
        var (_, pubB) = _gen.GenerateEcdhKeyPair();
        var invalidPriv = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }; // Kein gültiges PKCS#8

        var ex = Assert.Throws<ArgumentException>(() => _sut.DeriveSharedSecret(invalidPriv, pubB));
        Assert.Contains("Ungültiges PKCS#8", ex.Message);
    }

    [Fact]
    public void DeriveSharedSecret_InvalidPublicKeyFormat_ThrowsArgumentException()
    {
        var (privA, _) = _gen.GenerateEcdhKeyPair();
        var invalidPub = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }; // Kein gültiges SPKI

        var ex = Assert.Throws<ArgumentException>(() => _sut.DeriveSharedSecret(privA, invalidPub));
        Assert.Contains("Ungültiges SPKI", ex.Message);
    }

    [Fact]
    public void DeriveSharedSecret_WithWrongKeyType_ThrowsArgumentException()
    {
        // Versuch, einen Signatur-Key (ECDSA) für Key Agreement (ECDH) zu nutzen.
        // Technisch sind beides EC-Keys auf P-256, aber .NET unterscheidet streng zwischen 
        // ECDsa- und ECDiffieHellman-Klassen beim Import.
        // Hinweis: In neueren .NET Versionen ist der Import oft flexibler, aber der Algorithmus-Check
        // in der Klasse sollte greifen oder der Import fehlschlagen.
        
        var (signPriv, _) = _gen.GenerateSignKeyPair();
        var (_, ecdhPub) = _gen.GenerateEcdhKeyPair();

        // Je nach .NET Version und Implementierung könnte das funktionieren (da gleiche Kurve),
        // oder fehlschlagen. Unsere Implementierung nutzt `ECDiffieHellman.Create()`, 
        // was spezifisch für ECDH ist.
        
        // Wenn es hier NICHT wirft, ist das okay (dann akzeptiert .NET generische EC-Keys),
        // aber wenn wir explizit ungültige Daten (z.B. RSA Key) nehmen, muss es knallen.
        
        using var rsa = RSA.Create();
        var rsaPriv = rsa.ExportPkcs8PrivateKey();
        
        Assert.Throws<ArgumentException>(() => _sut.DeriveSharedSecret(rsaPriv, ecdhPub));
    }
}
