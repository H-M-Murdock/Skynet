using System.Text;
using Skynet.Core;
using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;

namespace Skynet.Tests.ResourceProvider;

public sealed class FileSystemResourceReaderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly TenantId _tenant = TenantId.New();
    private readonly FileSystemResourceReader _reader;

    public FileSystemResourceReaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Skynet_FS_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        
        // Tenant Ordner anlegen (implizit via WriteAtomicAsync im Helper, aber sicherheitshalber:)
        Directory.CreateDirectory(Path.Combine(_tempRoot, _tenant.ToString()));

        _reader = new FileSystemResourceReader(_tempRoot, priority: 10);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task TryGetAsync_Should_Return_Content_When_File_Exists()
    {
        // Arrange
        var key = "config/app.json";
        var content = "{\"foo\":1}";
        
        // Wir nutzen IoUtilities.WriteAtomicAsync zum Erstellen (Integrationstest!)
        await IoUtilities.WriteAtomicAsync(
            _tempRoot, 
            _tenant.ToString(), 
            key, 
            Encoding.UTF8.GetBytes(content));

        var req = new ResourceRequest(_tenant, key, ResourceKind.Config);

        // Act
        var result = await _reader.TryGetAsync(req);

        try // Manuelles Try-Finally Block für sicheres Disposen der Resource
        {
            // Assert
            Assert.Equal(ResourceLookupStatus.Found, result.Status);
            Assert.NotNull(result.Resource);
            Assert.Equal("application/json", result.Resource!.ContentType);
            Assert.NotNull(result.Resource.Version);
            
            using var sr = new StreamReader(result.Resource.Content);
            Assert.Equal(content, await sr.ReadToEndAsync());
        }
        finally
        {
            // Wichtig: Das eigentliche Resource-Objekt muss disposed werden
            result.Resource?.Dispose();
        }
    }

    [Fact]
    public async Task TryGetAsync_Should_Return_NotFound_When_Missing()
    {
        var req = new ResourceRequest(_tenant, "missing.txt", ResourceKind.File);
        var result = await _reader.TryGetAsync(req);
        Assert.Equal(ResourceLookupStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task TryGetAsync_Should_Return_NotFound_On_Security_Violation()
    {
        // IoUtilities wirft InvalidOperationException bei ".."
        // Der Reader sollte das fangen und NotFound (oder Error) zurückgeben, statt zu crashen.
        
        var req = new ResourceRequest(_tenant, "../secret.txt", ResourceKind.File);

        // Act
        var result = await _reader.TryGetAsync(req);

        // Assert
        // NotFound ist hier ok, da der Pfad aus Sicht des Tenants "nicht existiert" (da verboten).
        // Wichtig ist, dass keine Exception fliegt.
        Assert.Equal(ResourceLookupStatus.NotFound, result.Status);
        Assert.Contains("validation failed", result.Reason ?? "");
    }
    
    [Fact]
    public async Task TryGetAsync_Should_Handle_Invalid_Characters()
    {
        // IoUtilities erlaubt nur A-Z 0-9 _ - . /
        // Ein Key mit "$" oder " " sollte abgelehnt werden.
        
        var req = new ResourceRequest(_tenant, "invalid char$.txt", ResourceKind.File);

        var result = await _reader.TryGetAsync(req);

        Assert.Equal(ResourceLookupStatus.NotFound, result.Status);
        Assert.Contains("validation failed", result.Reason ?? "");
    }

    [Fact]
    public async Task ListKeysAsync_Should_Filter_By_Prefix()
    {
        // Arrange
        var k1 = "logs/2023.log";
        var k2 = "logs/2024.log";
        var k3 = "config.xml";
        
        await CreateFile(k1);
        await CreateFile(k2);
        await CreateFile(k3);

        var req = new ResourceRequest(_tenant, "logs/", ResourceKind.File);

        // Act
        var (keys, _) = await _reader.ListKeysAsync(req);

        // Assert
        Assert.Equal(2, keys.Count);
        Assert.Contains(k1, keys);
        Assert.Contains(k2, keys);
        Assert.DoesNotContain(k3, keys);
    }

    [Fact]
    public async Task ListKeysAsync_Should_Normalize_Slashes()
    {
        // Arrange
        // IoUtilities/Windows nutzen evtl Backslashes, wir wollen aber Slashes im Result.
        await CreateFile("sub/folder/item.txt");
        
        var req = new ResourceRequest(_tenant, "", ResourceKind.File);

        // Act
        var (keys, _) = await _reader.ListKeysAsync(req);

        // Assert
        Assert.Single(keys);
        Assert.Equal("sub/folder/item.txt", keys[0]); // Slash Check
    }

    // Helper Wrapper
    private Task CreateFile(string key) 
        => IoUtilities.WriteAtomicAsync(_tempRoot, _tenant.ToString(), key, Encoding.UTF8.GetBytes("data"));
}
