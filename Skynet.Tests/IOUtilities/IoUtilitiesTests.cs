using System.Text;
using Skynet.Core;
// ... existing code ...
using Xunit;

namespace Skynet.Tests.IOUtilities;

public class IoUtilitiesTests
{
    private readonly string _tempRoot;

    public IoUtilitiesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "IoUtilitiesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    ~IoUtilitiesTests()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void BuildSafeFullPath_ValidInputs_ReturnsPathUnderRoot()
    {
        var tenant = "tenantA";
        var key = "folder/sub/item.json";

        var full = IoUtilities.BuildSafeFullPath(_tempRoot, tenant, key);

        var rootWithSep = Path.GetFullPath(_tempRoot) + Path.DirectorySeparatorChar;
        Assert.StartsWith(rootWithSep, full, StringComparison.OrdinalIgnoreCase);

        var expectedSuffix = Path.Combine(tenant, "folder", "sub", "item.json");
        Assert.EndsWith(expectedSuffix, full, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(tenant, "folder", "sub"), full, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSafeFullPath_NormalizesBackslashesAndValidates()
    {
        var tenant = "t1";
        var key = @"a\b\c.txt";

        var full = IoUtilities.BuildSafeFullPath(_tempRoot, tenant, key, subFolder: "static");

        var expectedSuffix = Path.Combine(tenant, "static", "a", "b", "c.txt");
        Assert.EndsWith(expectedSuffix, full, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("./a.txt")]
    [InlineData("a/../../b.txt")]
    [InlineData(" withspace.txt")]
    [InlineData("a\\..\\b.txt")] // wird zu a/../b.txt
    [InlineData("a?b.txt")]
    public void BuildSafeFullPath_InvalidKeys_Throws(string key)
    {
        var tenant = "t1";
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(_tempRoot, tenant, key));
    }

    [Fact]
    public async Task OpenReadWithHashAsync_ReturnsStreamAndSha256Hex()
    {
        // Arrange
        var tenant = "t1";
        var key = "file.txt";
        var path = IoUtilities.BuildSafeFullPath(_tempRoot, tenant, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = "hello world";
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);

        // Erwarteten Hash exakt der Datei berechnen (nicht nur aus content-String)
        string expectedHash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
        {
            var hash = await sha.ComputeHashAsync(fs);
            expectedHash = Convert.ToHexString(hash);
        }

        var (stream, etag, fi) = await IoUtilities.OpenReadWithHashAsync(path);
        await using var _ = stream;

        // Assert
        Assert.True(fi.Exists);
        Assert.Equal(new FileInfo(path).Length, fi.Length);
        Assert.Equal(expectedHash, etag);

        using var sr = new StreamReader(stream, Encoding.UTF8, true, 1024, false);
        var read = await sr.ReadToEndAsync();
        Assert.Equal(content, read);
    }

    [Theory]
    [InlineData("a.json", "application/json")]
    [InlineData("a.yaml", "application/yaml")]
    [InlineData("a.yml", "application/yaml")]
    [InlineData("a.xml", "application/xml")]
    [InlineData("a.html", "text/html; charset=utf-8")]
    [InlineData("a.css", "text/css; charset=utf-8")]
    [InlineData("a.js", "application/javascript")]
    [InlineData("a.txt", "text/plain; charset=utf-8")]
    [InlineData("a.csv", "text/csv; charset=utf-8")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.jpg", "image/jpeg")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.pdf", "application/pdf")]
    [InlineData("a.zip", "application/zip")]
    [InlineData("a.gz", "application/gzip")]
    [InlineData("a.tar", "application/x-tar")]
    [InlineData("a.rar", "application/vnd.rar")]
    [InlineData("a.7z", "application/x-7z-compressed")]
    [InlineData("a.pem", "application/x-pem-file")]
    [InlineData("a.crt", "application/x-x509-ca-cert")]
    [InlineData("a.cer", "application/x-x509-ca-cert")]
    [InlineData("a.pfx", "application/x-pkcs12")]
    [InlineData("a.p12", "application/x-pkcs12")]
    [InlineData("a.unknown", null)]
    public void GuessContentType_ReturnsExpected(string file, string? expected)
    {
        var actual = IoUtilities.GuessContentType(file);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task OpenReadWithHashAsync_HonorsCancellationToken()
    {
        var path = Path.Combine(_tempRoot, "t1", "big.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Datei mit etwas Größe erzeugen
        var data = new byte[2 * 1024 * 1024];
        new Random(42).NextBytes(data);
        await File.WriteAllBytesAsync(path, data);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // sofortiger Abbruch

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await IoUtilities.OpenReadWithHashAsync(path, cts.Token);
        });
    }

    [Fact]
    public async Task OpenReadWithHashAsync_NonExistingFile_ThrowsFileNotFound()
    {
        var fakePath = Path.Combine(_tempRoot, "t1", "missing", "nope.txt");
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await IoUtilities.OpenReadWithHashAsync(fakePath);
        });
        Assert.Equal(fakePath, ex.FileName);
    }

    [Theory]
    [InlineData(null, "tenant", "a/b.txt", "baseRootFull")]
    [InlineData("", "tenant", "a/b.txt", "baseRootFull")]
    [InlineData(" ", "tenant", "a/b.txt", "baseRootFull")]
    [InlineData("C:\\tmp", null, "a/b.txt", "tenantIdString")]
    [InlineData("C:\\tmp", "", "a/b.txt", "tenantIdString")]
    [InlineData("C:\\tmp", " ", "a/b.txt", "tenantIdString")]
    [InlineData("C:\\tmp", "tenant", null, "key")]
    [InlineData("C:\\tmp", "tenant", "", "key")]
    [InlineData("C:\\tmp", "tenant", " ", "key")]
    public void BuildSafeFullPath_NullOrWhitespace_Throws(string? baseRoot, string? tenant, string? key, string expectedParam)
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            IoUtilities.BuildSafeFullPath(baseRoot!, tenant!, key!));
        Assert.Equal(expectedParam, ex.ParamName);
    }
}