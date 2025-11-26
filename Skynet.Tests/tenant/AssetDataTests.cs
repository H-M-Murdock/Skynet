using Skynet.Core.Tenant;

namespace Skynet.Tests.tenant;

public class AssetDataTests
{
    [Fact]
    public void Dispose_Should_Close_Underlying_Stream()
    {
        // Arrange
        var ms = new MemoryStream();
        var sut = new AssetData(ms, "text/plain", "test.txt");

        // Act
        sut.Dispose();

        // Assert
        // Zugriff auf geschlossenen Stream wirft ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => ms.WriteByte(1));
    }

    [Fact]
    public void Constructor_Should_Set_Properties_Correctly()
    {
        // Arrange
        using var ms = new MemoryStream();
        
        // Act
        var sut = new AssetData(ms, "application/pdf", "doc.pdf");

        // Assert
        Assert.Same(ms, sut.Stream);
        Assert.Equal("application/pdf", sut.ContentType);
        Assert.Equal("doc.pdf", sut.FileName);
    }
}
