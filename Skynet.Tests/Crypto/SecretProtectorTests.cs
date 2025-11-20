using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Crypto;
using Xunit;

namespace Skynet.Tests.Crypto;

public class SecretProtectorTests
{
    private readonly AesGcmAead _aead;
    private readonly byte[] _key;
    private readonly SecretProtector _sut;

    public SecretProtectorTests()
    {
        _aead = new AesGcmAead();
        _key = RandomNumberGenerator.GetBytes(32); // AES-256
        _sut = new SecretProtector(_aead, _key);
    }

    [Fact]
    public void Protect_Unprotect_String_Roundtrip_Success()
    {
        var plain = "Hello World! äöü €";
        
        var cipherBase64 = _sut.Protect(plain);
        var decrypted = _sut.Unprotect(cipherBase64);

        Assert.NotEqual(plain, cipherBase64); // Cipher muss anders aussehen
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Protect_Unprotect_String_WithAAD_Roundtrip_Success()
    {
        var plain = "Sensitive Config";
        var aad = "tenant-1";

        var cipherBase64 = _sut.Protect(plain, aad);
        var decrypted = _sut.Unprotect(cipherBase64, aad);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Unprotect_String_WithWrongAAD_ThrowsCryptographicException()
    {
        var plain = "Sensitive Config";
        var aad = "tenant-1";
        var wrongAad = "tenant-2";

        var cipherBase64 = _sut.Protect(plain, aad);

        // AAD mismatch -> Auth Tag Fail
        Assert.ThrowsAny<CryptographicException>(() => _sut.Unprotect(cipherBase64, wrongAad));
    }

    [Fact]
    public void Protect_Unprotect_Bytes_Roundtrip_Success()
    {
        var plain = new byte[] { 1, 2, 3, 4, 255 };
        
        var packed = _sut.Protect(plain);
        var decrypted = _sut.Unprotect(packed);

        // Packed format size: Nonce (12) + Tag (16) + Cipher (5) = 33 bytes
        Assert.Equal(12 + 16 + plain.Length, packed.Length);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Unprotect_Bytes_Tampered_ThrowsCryptographicException()
    {
        var plain = new byte[] { 1, 2, 3 };
        var packed = _sut.Protect(plain);

        // Manipuliere das letzte Byte (Teil des Ciphertexts oder Tags)
        packed[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => _sut.Unprotect(packed));
    }

    [Fact]
    public void Unprotect_Bytes_TooShort_ThrowsArgumentException()
    {
        // Muss mindestens Nonce (12) + Tag (16) lang sein
        var tooShort = new byte[20]; 
        
        Assert.Throws<ArgumentException>(() => _sut.Unprotect(tooShort));
    }

    [Fact]
    public async Task ProtectAsync_UnprotectAsync_String_Roundtrip()
    {
        var plain = "Async Test Data";
        
        var cipherBase64 = await _sut.ProtectAsync(plain);
        var decrypted = await _sut.UnprotectAsync(cipherBase64);

        Assert.Equal(plain, decrypted);
    }
    
    [Fact]
    public async Task ProtectAsync_UnprotectAsync_String_WithAAD_Roundtrip()
    {
        var plain = "Async Test Data With AAD";
        var aadBytes = Encoding.UTF8.GetBytes("context");

        var cipherBase64 = await _sut.ProtectAsync(plain, aadBytes);
        var decrypted = await _sut.UnprotectAsync(cipherBase64, aadBytes);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Protect_Stream_Roundtrip_Success()
    {
        var plainData = Encoding.UTF8.GetBytes("Stream Content");
        using var sourceStream = new MemoryStream(plainData);

        // Protect
        using var cipherStream = _sut.Protect(sourceStream);
        
        // CipherStream sollte am Anfang stehen
        Assert.Equal(0, cipherStream.Position);
        Assert.True(cipherStream.Length > plainData.Length);

        // Unprotect
        // WICHTIG: Wir müssen sicher sein, dass wir einen MemoryStream zurückbekommen,
        // oder generisch lesen. SecretProtector gibt MemoryStream zurück.
        using var decryptedStream = (MemoryStream)_sut.Unprotect(cipherStream);
        
        var decryptedData = decryptedStream.ToArray();

        Assert.Equal(plainData, decryptedData);
    }

    [Fact]
    public async Task ProtectAsync_Stream_To_Destination_Roundtrip()
    {
        var plainData = RandomNumberGenerator.GetBytes(1000);
        using var sourceStream = new MemoryStream(plainData);
        using var encryptedStream = new MemoryStream();
        using var decryptedStream = new MemoryStream();

        // Protect: Source -> EncryptedStream
        await _sut.ProtectAsync(sourceStream, encryptedStream);
        
        encryptedStream.Position = 0; // Reset für Lesen

        // Unprotect: EncryptedStream -> DecryptedStream
        await _sut.UnprotectAsync(encryptedStream, decryptedStream);

        decryptedStream.Position = 0;
        var result = decryptedStream.ToArray();

        Assert.Equal(plainData, result);
    }
    
    [Fact]
    public void Constructor_NullArgs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SecretProtector(null!, _key));
        Assert.Throws<ArgumentNullException>(() => new SecretProtector(_aead, null!));
    }
}