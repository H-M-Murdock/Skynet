using System.Security.Cryptography;
using System.Text;
using Moq;
using Skynet.Core.Crypto;
using Skynet.Core.Licensing;
using Skynet.Core.Tenant;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Licensing;

public class LicenseVerifierTests
{
    private readonly Mock<ISignature> _signatureMock;
    private readonly Mock<IClock> _clockMock;
    private readonly ILicenseCanonicalizer _canonicalizer;
    private readonly LicenseVerifier _verifier;
    private readonly byte[] _dummyPublicKey = new byte[] { 0x01, 0x02, 0x03 };

    public LicenseVerifierTests()
    {
        _signatureMock = new Mock<ISignature>();
        _clockMock = new Mock<IClock>();
        _canonicalizer = new JsonLicenseCanonicalizer(); // Use real canonicalizer as it's logic-heavy
        
        _verifier = new LicenseVerifier(
            _signatureMock.Object,
            _dummyPublicKey,
            _clockMock.Object,
            _canonicalizer
        );
    }

    [Fact]
    public async Task VerifyAsync_ValidLicense_ReturnsLicenseInfo()
    {
        // Arrange
        var now = DateTime.UtcNow; // IClock.UtcNow returns DateTime
        _clockMock.Setup(c => c.UtcNow).Returns(now);

        // Konvertierung in DateTimeOffset für die Envelope
        var nowOffset = new DateTimeOffset(now);
        var envelope = CreateValidEnvelope(nowOffset.AddHours(-1), nowOffset.AddHours(1));
        
        // Signature is always valid in this test case
        _signatureMock
            .Setup(s => s.Verify(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
            .Returns(true);

        // Act
        var result = await _verifier.VerifyAsync(envelope);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(envelope.TenantId, result.TenantId);
        Assert.True(result.SignatureValid);
        Assert.NotNull(result.EnvelopeFingerprint);
    }

    [Fact]
    public async Task VerifyAsync_ExpiredLicense_ThrowsCryptographicException()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _clockMock.Setup(c => c.UtcNow).Returns(now);

        var nowOffset = new DateTimeOffset(now);
        // Expired 1 hour ago
        var envelope = CreateValidEnvelope(nowOffset.AddHours(-2), nowOffset.AddHours(-1));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CryptographicException>(() => _verifier.VerifyAsync(envelope));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_NotYetValidLicense_ThrowsCryptographicException()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _clockMock.Setup(c => c.UtcNow).Returns(now);

        var nowOffset = new DateTimeOffset(now);
        // Valid in 1 hour
        var envelope = CreateValidEnvelope(nowOffset.AddHours(1), nowOffset.AddHours(2));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CryptographicException>(() => _verifier.VerifyAsync(envelope));
        Assert.Contains("not yet valid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_InvalidSignature_ThrowsCryptographicException()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _clockMock.Setup(c => c.UtcNow).Returns(now);
        
        var nowOffset = new DateTimeOffset(now);
        var envelope = CreateValidEnvelope(nowOffset.AddHours(-1), nowOffset.AddHours(1));

        // Signature fail
        _signatureMock
            .Setup(s => s.Verify(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
            .Returns(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CryptographicException>(() => _verifier.VerifyAsync(envelope));
        Assert.Contains("signature invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LicenseEnvelope CreateValidEnvelope(DateTimeOffset issued, DateTimeOffset expires)
    {
        return new LicenseEnvelope(
            tenantId: new TenantId(Guid.NewGuid()),
            serverPubEcdh: new byte[32],
            nonceClient: new byte[12],
            nonceServer: new byte[12],
            featureFlags: new Dictionary<string, string> { { "FeatureA", "true" } },
            issuedAtUtc: issued,
            notBeforeUtc: issued, // Valid from issue date
            expiresUtc: expires,
            kemAlg: KemAlgorithm.X25519,
            kdfAlg: KdfAlgorithm.HkdfSha256,
            aeadAlg: AeadAlgorithm.Aes256Gcm,
            signKeyId: "key-1",
            signature: new byte[64]
        );
    }
}
