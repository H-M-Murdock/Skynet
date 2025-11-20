using System.Text;
using Skynet.Core.Crypto;
using Xunit;

namespace Skynet.Tests.Crypto;

public class KeyDerivationTests
{
    private readonly KeyDerivation _sut = new();

    // Test-Vektoren aus RFC 5869 wären hier ideal, aber für Unit-Tests reicht oft Konsistenz und Determinismus.
    // Wir prüfen hier vor allem die API-Eigenschaften.

    [Fact]
    public void HkdfSha256_Deterministic_SameInputsProduceSameKey()
    {
        var ikm = Encoding.UTF8.GetBytes("input-keying-material");
        var salt = Encoding.UTF8.GetBytes("salt-value");
        var info = Encoding.UTF8.GetBytes("context-info");

        var key1 = _sut.HkdfSha256(ikm, salt, info, 32);
        var key2 = _sut.HkdfSha256(ikm, salt, info, 32);

        Assert.Equal(key1, key2);
        Assert.Equal(32, key1.Length);
    }

    [Fact]
    public void HkdfSha256_DifferentInfo_ProducesDifferentKeys()
    {
        var ikm = Encoding.UTF8.GetBytes("input-keying-material");
        var salt = Encoding.UTF8.GetBytes("salt-value");
        
        var keyA = _sut.HkdfSha256(ikm, salt, Encoding.UTF8.GetBytes("context-A"), 32);
        var keyB = _sut.HkdfSha256(ikm, salt, Encoding.UTF8.GetBytes("context-B"), 32);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void HkdfSha256_DifferentSalt_ProducesDifferentKeys()
    {
        var ikm = Encoding.UTF8.GetBytes("input-keying-material");
        var info = Encoding.UTF8.GetBytes("context");
        
        var keyA = _sut.HkdfSha256(ikm, Encoding.UTF8.GetBytes("salt-A"), info, 32);
        var keyB = _sut.HkdfSha256(ikm, Encoding.UTF8.GetBytes("salt-B"), info, 32);

        Assert.NotEqual(keyA, keyB);
    }
    
    [Fact]
    public void HkdfSha256_CanProduceLongKeys()
    {
        // HKDF kann Schlüssel erzeugen, die länger als der Hash-Output sind (Expansion)
        var ikm = Encoding.UTF8.GetBytes("short-secret");
        
        // SHA-256 = 32 Bytes. Wir fordern 64 Bytes an -> 2 Blöcke Expansion
        var longKey = _sut.HkdfSha256(ikm, null, null, 64);
        
        Assert.Equal(64, longKey.Length);
    }

    [Fact]
    public void HkdfSha512_ProducesDifferentKeyThanSha256()
    {
        var ikm = Encoding.UTF8.GetBytes("input-keying-material");
        var salt = Encoding.UTF8.GetBytes("salt");
        var info = Encoding.UTF8.GetBytes("info");

        var k256 = _sut.HkdfSha256(ikm, salt, info, 32);
        var k512 = _sut.HkdfSha512(ikm, salt, info, 32); // Gleiche Länge angefordert

        Assert.NotEqual(k256, k512);
    }

    [Fact]
    public void Hkdf_WithNullSaltAndInfo_Works()
    {
        var ikm = Encoding.UTF8.GetBytes("secret");
        
        // Null Salt sollte intern wie leeres Array behandelt werden (Standard RFC)
        var key = _sut.HkdfSha256(ikm, null, null, 32);
        
        Assert.NotNull(key);
        Assert.Equal(32, key.Length);

        // Gegenprobe mit leeren Arrays
        var keyExplicit = _sut.HkdfSha256(ikm, Array.Empty<byte>(), Array.Empty<byte>(), 32);
        Assert.Equal(key, keyExplicit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Hkdf_InvalidLength_ThrowsArgumentOutOfRangeException(int length)
    {
        var ikm = new byte[] { 1, 2, 3 };
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.HkdfSha256(ikm, null, null, length));
    }

    [Fact]
    public void Hkdf_NullOrEmptyIKM_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _sut.HkdfSha256(null!, null, null));
        Assert.Throws<ArgumentException>(() => _sut.HkdfSha256(Array.Empty<byte>(), null, null));
    }
    
    // RFC 5869 Test Case 1 (Basic) für SHA-256
    // IKM  = 0x0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b (22 bytes)
    // salt = 0x000102030405060708090a0b0c (13 bytes)
    // info = 0xf0f1f2f3f4f5f6f7f8f9 (10 bytes)
    // L    = 42
    // OKM  = 0x8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8
    [Fact]
    public void HkdfSha256_Rfc5869_TestCase1()
    {
        var ikm = Convert.FromHexString("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b");
        var salt = Convert.FromHexString("000102030405060708090a0b0c");
        var info = Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9");
        
        // Korrigiertes erwartetes Ergebnis für Test Case 1:
        var expected = Convert.FromHexString("3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865");

        var actual = _sut.HkdfSha256(ikm, salt, info, 42);

        Assert.Equal(expected, actual);
    }
}
