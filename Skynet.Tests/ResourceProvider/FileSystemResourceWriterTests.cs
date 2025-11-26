using System.Text;
using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;
// Echte IoUtilities

namespace Skynet.Tests.ResourceProvider;

public sealed class FileSystemResourceWriterTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly TenantId _tenant = TenantId.New();
    private readonly FileSystemResourceWriter _writer;

    public FileSystemResourceWriterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Skynet_FS_WriteTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        
        _writer = new FileSystemResourceWriter(_tempRoot, priority: 10);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task WriteAsync_Should_Create_File_And_Return_Correct_Metadata()
    {
        // Arrange
        var key = "assets/logo.png";
        var contentBytes = Encoding.UTF8.GetBytes("fake-image-content");
        using var stream = new MemoryStream(contentBytes);

        var req = new ResourceRequest(_tenant, key, ResourceKind.Asset);

        // Act
        var result = await _writer.WriteAsync(req, stream);

        // Assert
        Assert.Equal(key, result.Key);
        Assert.NotNull(result.Version); // Etag berechnet
        Assert.Equal(contentBytes.Length, result.ContentLength);
        Assert.Equal("image/png", result.ContentType); // Guessed from extension

        // Verify Disk
        var fullPath = Path.Combine(_tempRoot, _tenant.ToString(), "assets", "logo.png");
        Assert.True(File.Exists(fullPath));
        var diskContent = await File.ReadAllBytesAsync(fullPath);
        Assert.Equal(contentBytes, diskContent);
    }

    [Fact]
    public async Task WriteAsync_Should_Overwrite_Atomically()
    {
        // Arrange: Datei existiert schon
        var key = "config.json";
        var req = new ResourceRequest(_tenant, key, ResourceKind.Config);

        using (var s1 = new MemoryStream(Encoding.UTF8.GetBytes("v1")))
            await _writer.WriteAsync(req, s1);

        // Act: Überschreiben mit v2
        using (var s2 = new MemoryStream(Encoding.UTF8.GetBytes("v2-updated")))
        {
            var result = await _writer.WriteAsync(req, s2);
            Assert.NotNull(result.Version);
        }

        // Assert
        var fullPath = Path.Combine(_tempRoot, _tenant.ToString(), key);
        var text = await File.ReadAllTextAsync(fullPath);
        Assert.Equal("v2-updated", text);
    }

    [Fact]
    public async Task WriteAsync_Should_Respect_IfMatch_Success()
    {
        // Arrange
        var key = "data.txt";
        var req = new ResourceRequest(_tenant, key, ResourceKind.File);

        // V1 schreiben
        IResourceWriteResult v1Result;
        using (var s = new MemoryStream(Encoding.UTF8.GetBytes("A")))
            v1Result = await _writer.WriteAsync(req, s);

        // Act: V2 schreiben MIT korrektem ETag
        using (var s = new MemoryStream(Encoding.UTF8.GetBytes("B")))
        {
            var v2Result = await _writer.WriteAsync(req, s, ifMatch: v1Result.Version);
            Assert.NotEqual(v1Result.Version, v2Result.Version);
        }
    }

    [Fact]
    public async Task WriteAsync_Should_Throw_On_IfMatch_Mismatch()
    {
        // Arrange
        var key = "data.txt";
        var req = new ResourceRequest(_tenant, key, ResourceKind.File);

        using (var s = new MemoryStream(Encoding.UTF8.GetBytes("A")))
            await _writer.WriteAsync(req, s);

        // Act & Assert: V2 schreiben mit FALSCHEM ETag
        using (var s = new MemoryStream(Encoding.UTF8.GetBytes("B")))
        {
            await Assert.ThrowsAsync<IOException>(async () => 
                await _writer.WriteAsync(req, s, ifMatch: "invalid-etag"));
        }
    }

    [Fact]
    public async Task WriteAsync_Should_Reject_Secret_ResourceKind()
    {
        var req = new ResourceRequest(_tenant, "my.secret", ResourceKind.Secret);
        using var s = new MemoryStream(new byte[1]);

        await Assert.ThrowsAsync<NotSupportedException>(async () => 
            await _writer.WriteAsync(req, s));
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_File()
    {
        // Arrange
        var key = "del.txt";
        var req = new ResourceRequest(_tenant, key, ResourceKind.File);
        using (var s = new MemoryStream(Encoding.UTF8.GetBytes("bye")))
            await _writer.WriteAsync(req, s);

        // Act
        var result = await _writer.DeleteAsync(req);

        // Assert
        Assert.True(result.Deleted);
        Assert.NotNull(result.PreviousVersion); // Sollte den ETag vor dem Löschen enthalten
        
        var fullPath = Path.Combine(_tempRoot, _tenant.ToString(), key);
        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteAsync_Should_Be_Idempotent()
    {
        // Arrange
        var req = new ResourceRequest(_tenant, "never_existed.txt", ResourceKind.File);

        // Act
        var result = await _writer.DeleteAsync(req);

        // Assert
        Assert.False(result.Deleted);
    }
}
