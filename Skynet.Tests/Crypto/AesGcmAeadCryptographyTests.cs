using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Crypto;
using Xunit;

namespace Skynet.Tests.Crypto;

public class AesGcmAeadTests
{
    private readonly AesGcmAead _sut = new();

    [Fact]
    public void Properties_ReturnCorrectConstants()
    {
        Assert.Equal(12, _sut.ExpectedNonceSizeBytes);
        Assert.Equal(16, _sut.ExpectedTagSizeBytes);
    }

    [Theory]
    [InlineData(16)] // AES-128
    [InlineData(24)] // AES-192
    [InlineData(32)] // AES-256
    public void Encrypt_Decrypt_Roundtrip_Success(int keySize)
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(keySize);
        var plaintext = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog");
        
        // Act
        // Automatische Nonce-Generierung
        var (nonce, tag, cipher) = _sut.Encrypt(key, plaintext);
        var decrypted = _sut.Decrypt(key, nonce, tag, cipher);

        // Assert
        Assert.Equal(plaintext, decrypted);
        Assert.Equal(12, nonce.Length);
        Assert.Equal(16, tag.Length);
    }

    [Fact]
    public void Encrypt_Decrypt_WithAAD_Roundtrip_Success()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("Secret Data with Context");
        var aad = Encoding.UTF8.GetBytes("tenant-id:12345");

        // Act
        var (nonce, tag, cipher) = _sut.Encrypt(key, plaintext, aad);
        var decrypted = _sut.Decrypt(key, nonce, tag, cipher, aad);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_WithSpecificNonce_UsesProvidedNonce()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        // WICHTIG: Wir müssen 'aad: null' explizit angeben, damit der Compiler 
        // die Überladung (Key, Nonce, Plaintext, Aad) wählt und nicht (Key, Plaintext, Aad).
        var (tag, cipher) = _sut.Encrypt(key, nonce, plaintext, aad: null);
        
        // Assert
        // Entschlüsseln mit denselben Parametern.
        var decrypted = _sut.Decrypt(key, nonce, tag, cipher);
        
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptographicException()
    {
        // Arrange
        var key1 = RandomNumberGenerator.GetBytes(32);
        var key2 = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("Data");

        var (nonce, tag, cipher) = _sut.Encrypt(key1, plaintext);

        // Act & Assert
        // AuthenticationTagMismatchException ist eine CryptographicException
        Assert.ThrowsAny<CryptographicException>(() => 
            _sut.Decrypt(key2, nonce, tag, cipher));
    }

    [Fact]
    public void Decrypt_WithTamperedCipher_ThrowsCryptographicException()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("Data integrity check");
        var (nonce, tag, cipher) = _sut.Encrypt(key, plaintext);

        // Tamper: Ein Byte im Ciphertext ändern
        cipher[0] ^= 0xFF; 

        // Act & Assert
        Assert.ThrowsAny<CryptographicException>(() => 
            _sut.Decrypt(key, nonce, tag, cipher));
    }

    [Fact]
    public void Decrypt_WithTamperedTag_ThrowsCryptographicException()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("Data");
        var (nonce, tag, cipher) = _sut.Encrypt(key, plaintext);

        // Tamper: Tag verändern
        tag[0] ^= 0xFF;

        // Act & Assert
        Assert.ThrowsAny<CryptographicException>(() => 
            _sut.Decrypt(key, nonce, tag, cipher));
    }

    [Fact]
    public void Decrypt_WithWrongAAD_ThrowsCryptographicException()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("Data");
        var correctAad = Encoding.UTF8.GetBytes("Context A");
        var wrongAad = Encoding.UTF8.GetBytes("Context B");

        var (nonce, tag, cipher) = _sut.Encrypt(key, plaintext, correctAad);

        // Act & Assert
        // 1. Falscher AAD Kontext
        Assert.ThrowsAny<CryptographicException>(() => 
            _sut.Decrypt(key, nonce, tag, cipher, wrongAad));
        
        // 2. Kein AAD, obwohl einer erwartet wird
        Assert.ThrowsAny<CryptographicException>(() => 
            _sut.Decrypt(key, nonce, tag, cipher, null));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(33)]
    public void Encrypt_InvalidKeySize_ThrowsArgumentException(int invalidKeySize)
    {
        var key = new byte[invalidKeySize];
        var plain = new byte[10];
        
        Assert.Throws<ArgumentException>(() => _sut.Encrypt(key, plain));
    }

    [Fact]
    public void Decrypt_InvalidNonceSize_ThrowsArgumentException()
    {
        var key = new byte[32];
        var wrongNonce = new byte[11]; // Muss 12 sein
        var tag = new byte[16];
        var cipher = new byte[10];

        Assert.Throws<ArgumentException>(() => 
            _sut.Decrypt(key, wrongNonce, tag, cipher));
    }

    [Fact]
    public void Decrypt_InvalidTagSize_ThrowsArgumentException()
    {
        var key = new byte[32];
        var nonce = new byte[12];
        var wrongTag = new byte[15]; // Muss 16 sein
        var cipher = new byte[10];

        Assert.Throws<ArgumentException>(() => 
            _sut.Decrypt(key, nonce, wrongTag, cipher));
    }

    [Fact]
    public async Task EncryptAsync_DecryptAsync_Stream_Roundtrip()
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(32);
        var originalData = new byte[1024]; 
        RandomNumberGenerator.Fill(originalData);
        var aad = Encoding.UTF8.GetBytes("stream-context");

        using var plaintextStream = new MemoryStream(originalData);

        // Act - Encrypt
        var (nonce, tag, cipher) = await _sut.EncryptAsync(key, plaintextStream, aad);

        // Assert Encrypt
        Assert.Equal(originalData.Length, cipher.Length);

        // Act - Decrypt
        using var cipherStream = new MemoryStream(cipher);
        var decryptedData = await _sut.DecryptAsync(key, nonce, tag, cipherStream, aad);

        // Assert Decrypt
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public void Null_Arguments_ThrowArgumentNullException()
    {
        var key = new byte[32];
        var nonce = new byte[12];
        var tag = new byte[16];
        var plain = new byte[10];
        var cipher = new byte[10];

        Assert.Throws<ArgumentNullException>(() => _sut.Encrypt(null!, plain));
        Assert.Throws<ArgumentNullException>(() => _sut.Encrypt(key, (byte[])null!));
        Assert.Throws<ArgumentNullException>(() => _sut.Decrypt(key, nonce, tag, null!));
    }
}