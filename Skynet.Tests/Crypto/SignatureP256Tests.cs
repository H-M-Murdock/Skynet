using System.Text;
using Skynet.Core.Crypto;
using Xunit;

namespace Skynet.Tests.Crypto;

public class SignatureP256Tests
{
    private readonly SignatureP256 _sut;
    private readonly DefaultKeyPairGenerator _gen;

    public SignatureP256Tests()
    {
        _sut = new SignatureP256("key-1");
        _gen = new DefaultKeyPairGenerator();
    }

    [Fact]
    public void Properties_AreSetCorrectly()
    {
        Assert.Equal("key-1", _sut.KeyId);
        Assert.Equal(SignatureAlgorithm.ECDSA_P256, _sut.Algorithm);
    }

    [Fact]
    public void Sign_Verify_Roundtrip_Success()
    {
        // Arrange
        var (priv, pub) = _gen.GenerateSignKeyPair();
        var data = Encoding.UTF8.GetBytes("Signed Document Content");

        // Act
        var signature = _sut.Sign(priv, data);
        var isValid = _sut.Verify(pub, data, signature);

        // Assert
        Assert.NotNull(signature);
        Assert.NotEmpty(signature);
        Assert.True(isValid, "Gültige Signatur sollte verifiziert werden.");
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        // Arrange
        var (priv, pub) = _gen.GenerateSignKeyPair();
        var data = Encoding.UTF8.GetBytes("Original Data");
        var signature = _sut.Sign(priv, data);

        // Tamper Data
        var tamperedData = Encoding.UTF8.GetBytes("Original DatA"); // A statt a

        // Act
        var isValid = _sut.Verify(pub, tamperedData, signature);

        // Assert
        Assert.False(isValid, "Veränderte Daten dürfen nicht validieren.");
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        // Arrange
        var (priv, pub) = _gen.GenerateSignKeyPair();
        var data = Encoding.UTF8.GetBytes("Data");
        var signature = _sut.Sign(priv, data);

        // Tamper Signature (letztes Byte flippen)
        signature[^1] ^= 0xFF;

        // Act
        var isValid = _sut.Verify(pub, data, signature);

        // Assert
        Assert.False(isValid, "Veränderte Signatur darf nicht validieren.");
    }

    [Fact]
    public void Verify_WrongPublicKey_ReturnsFalse()
    {
        // Arrange
        var (privA, _) = _gen.GenerateSignKeyPair();
        var (_, pubB) = _gen.GenerateSignKeyPair(); // Anderes Paar
        var data = Encoding.UTF8.GetBytes("Data");

        // Sign mit Key A
        var signature = _sut.Sign(privA, data);

        // Act: Verify mit Key B
        var isValid = _sut.Verify(pubB, data, signature);

        // Assert
        Assert.False(isValid, "Falscher Public Key darf nicht validieren.");
    }

    [Fact]
    public void Sign_InvalidPrivateKey_ThrowsArgumentException()
    {
        var data = new byte[] { 1, 2, 3 };
        var invalidPriv = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        // Sollte Exception werfen, da Import fehlschlägt
        Assert.Throws<ArgumentException>(() => _sut.Sign(invalidPriv, data));
    }

    [Fact]
    public void Verify_InvalidPublicKey_ReturnsFalse()
    {
        var data = new byte[] { 1, 2, 3 };
        var signature = new byte[64]; // Dummy
        var invalidPub = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        // Verify sollte false zurückgeben, nicht crashen (laut Interface-Contract oft gewünscht, 
        // hier in Implementierung: catch CryptographicException -> false)
        var isValid = _sut.Verify(invalidPub, data, signature);
        
        Assert.False(isValid);
    }

    [Fact]
    public void Sign_NullArguments_ThrowsArgumentNullException()
    {
        var (priv, _) = _gen.GenerateSignKeyPair();
        
        Assert.Throws<ArgumentNullException>(() => _sut.Sign(priv, null!));
        Assert.Throws<ArgumentException>(() => _sut.Sign(null!, new byte[1]));
    }

    [Fact]
    public void Verify_NullArguments_ThrowsArgumentExceptionOrNull()
    {
        var (priv, pub) = _gen.GenerateSignKeyPair();
        var data = new byte[] { 1 };
        var sig = _sut.Sign(priv, data);

        Assert.Throws<ArgumentNullException>(() => _sut.Verify(pub, null!, sig));
        Assert.Throws<ArgumentException>(() => _sut.Verify(null!, data, sig));
        Assert.Throws<ArgumentException>(() => _sut.Verify(pub, data, null!));
    }
}
