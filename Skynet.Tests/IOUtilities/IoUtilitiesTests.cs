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

    [Fact]
    public void BuildSafeFullPath_WithSubFolder_CombinesCorrectly()
    {
        var full = IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", "a/b.txt", subFolder: "static");
        var expectedSuffix = Path.Combine("tenant1", "static", "a", "b.txt");
        Assert.EndsWith(expectedSuffix, full, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("sta tic")]
    [InlineData("..")]
    [InlineData("./")]
    [InlineData("sta/../tic")]
    public void BuildSafeFullPath_InvalidSubFolder_StillPreventEscape(string subFolder)
    {
        // Hinweis: Aktuell validiert BuildSafeFullPath subFolder nicht separat,
        // aber der Root-Escape wird am Ende verhindert. Erwartung: InvalidOperationException.
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", "a/b.txt", subFolder));
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

    [Theory]
    [InlineData("a/../b.txt")]      // relativ, enthält .. nach Normalisierung
    [InlineData(@".\..\b.txt")]     // wird zu ../b.txt
    [InlineData(@"a\..\..\b.txt")]  // wird zu a/../../b.txt
    public void BuildSafeFullPath_PathTraversal_Throws(string key)
    {
        var baseRoot = _tempRoot;
        var tenant = "tenant";
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(baseRoot, tenant, key));
    }

    [Fact]
    public void BuildSafeFullPath_AbsoluteKeyRejected()
    {
        var baseRoot = _tempRoot;
        var tenant = "tenant";

        var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "other", "abs.txt"));
        var key = absolute.Replace('\\', '/'); // simuliert absoluten Key (unerlaubt)

        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(baseRoot, tenant, key));
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
    
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("/")]
    [InlineData(@"\")]
    public void BuildSafeFullPath_Key_OnlyDotsOrSeparators_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", key));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("/")]
    [InlineData(@"\")]
    public void BuildSafeFullPath_SubFolder_OnlyDotsOrSeparators_Throws(string subFolder)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", "a/b.txt", subFolder));
    }

    [Fact]
    public void BuildSafeFullPath_VeryLongKey_StillUnderRoot()
    {
        var longName = new string('a', 200);
        var key = $"{longName}/{longName}/{longName}.txt";
        var full = IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", key);
        var rootWithSep = Path.GetFullPath(_tempRoot) + Path.DirectorySeparatorChar;
        Assert.StartsWith(rootWithSep, full, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("tenant1", longName, longName, $"{longName}.txt"), full, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSafeFullPath_VeryLongSubFolder_StillUnderRoot()
    {
        var longSeg = new string('b', 180);
        var sub = $"{longSeg}/{longSeg}";
        var full = IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", "file.txt", sub);
        var rootWithSep = Path.GetFullPath(_tempRoot) + Path.DirectorySeparatorChar;
        Assert.StartsWith(rootWithSep, full, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("tenant1", longSeg, longSeg, "file.txt"), full, StringComparison.OrdinalIgnoreCase);
    }
    
    [Theory]
    [InlineData("/static")]          // führender Slash -> absolutes Segment simuliert
    [InlineData("static/")]          // trailing Slash -> leeres Segment
    [InlineData("static//images")]   // leeres Segment in der Mitte
    [InlineData(@"\static")]         // führender Backslash -> nach Normalisierung "/static"
    [InlineData(@"static\")]         // trailing Backslash -> nach Normalisierung "static/"
    [InlineData(@"static\\images")]  // doppelter Backslash -> leeres Segment
    public void BuildSafeFullPath_SubFolder_EmptyOrAbsoluteSegments_Throws(string subFolder)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", "a/b.txt", subFolder));
    }

    [Theory]
    [InlineData("/a/b.txt")]         // führender Slash -> absolutes Segment simuliert
    [InlineData("a/b.txt/")]         // trailing Slash -> leeres Segment
    [InlineData("a//b.txt")]         // leeres Segment in der Mitte
    [InlineData(@"\a\b.txt")]        // führender Backslash -> nach Normalisierung "/a/b.txt"
    [InlineData(@"a\b.txt\")]        // trailing Backslash -> nach Normalisierung "a/b.txt/"
    [InlineData(@"a\\b.txt")]        // doppelter Backslash -> leeres Segment
    public void BuildSafeFullPath_Key_EmptyOrAbsoluteSegments_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", key));
    }

    [Theory]
    [InlineData("A_-9.x")]
    [InlineData("folder/Sub/name.json")]
    [InlineData(@"folder\sub\file.txt")] // Backslashes werden normalisiert
    public void BuildSafeFullPath_Key_ValidVariants_Pass(string key)
    {
        var full = IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", key);
        var rootWithSep = Path.GetFullPath(_tempRoot) + Path.DirectorySeparatorChar;
        Assert.StartsWith(rootWithSep, full, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("tenant1"), full, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteAtomicAsync_CreatesFileAndReturnsEtag()
    {
        var tenant = "t1";
        var key = "folder/new.txt";
        var bytes = Encoding.UTF8.GetBytes("hello write");

        var (fullPath, etag, fi) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, bytes);

        Assert.True(File.Exists(fullPath));
        Assert.Equal(fullPath, fi.FullName);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        Assert.Equal(expectedHash, etag);

        var read = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
        Assert.Equal("hello write", read);
    }

    [Fact]
    public async Task WriteAtomicAsync_OverwritesAtomically_UpdatesEtag()
    {
        var tenant = "t1";
        var key = "folder/replace.txt";

        var v1 = Encoding.UTF8.GetBytes("v1");
        var (p1, e1, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, v1);
        Assert.True(File.Exists(p1));

        var v2 = Encoding.UTF8.GetBytes("v2 - updated");
        var (p2, e2, fi2) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, v2);

        Assert.Equal(p1, p2);
        Assert.NotEqual(e1, e2);
        var expectedHash2 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(v2));
        Assert.Equal(expectedHash2, e2);
        Assert.True(fi2.Exists);
        var content = await File.ReadAllTextAsync(p2, Encoding.UTF8);
        Assert.Equal("v2 - updated", content);
    }

    [Fact]
    public async Task WriteAtomicAsync_WithSubFolder_WritesUnderSubFolder()
    {
        var (fullPath, _, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, "tenantX", "a/b.txt", Encoding.UTF8.GetBytes("x"), "static");
        Assert.EndsWith(Path.Combine("tenantX", "static", "a", "b.txt"), fullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "t", "k")]
    [InlineData("", "t", "k")]
    [InlineData(" ", "t", "k")]
    [InlineData("root", null, "k")]
    [InlineData("root", "", "k")]
    [InlineData("root", " ", "k")]
    [InlineData("root", "t", null)]
    [InlineData("root", "t", "")]
    [InlineData("root", "t", " ")]
    public async Task WriteAtomicAsync_NullOrWhitespace_Throws(string? baseRoot, string? tenant, string? key)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await IoUtilities.WriteAtomicAsync(baseRoot!, tenant!, key!, new byte[] { 1, 2 }));
    }

    [Fact]
    public async Task WriteAtomicAsync_HonorsCancellationToken()
    {
        var tenant = "t1";
        var key = "folder/cancel.txt";
        var data = new byte[512 * 1024];
        new Random(7).NextBytes(data);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // sofort abbrechen

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, data, ct: cts.Token));
    }

    [Fact]
    public async Task WriteAtomicAsync_ParallelWrites_ResultIsConsistent()
    {
        var tenant = "t1";
        var key = "folder/parallel.txt";

        var v1 = Encoding.UTF8.GetBytes("first");
        var v2 = Encoding.UTF8.GetBytes("second");

        // Zwei konkurrierende Writes
        var t1 = IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, v1);
        var t2 = IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, v2);

        var results = await Task.WhenAll(t1, t2);

        // Datei existiert und enthält entweder first oder second, aber konsistent
        var path = results[0].fullPath;
        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path, Encoding.UTF8);
        Assert.True(content is "first" or "second");

        // ETag entspricht dem tatsächlichen Inhalt
        var expectedEtag = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        Assert.Contains(results.Select(r => r.etag), e => string.Equals(e, expectedEtag, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/../../b.txt")]
    [InlineData("a//b.txt")]
    [InlineData("/abs.txt")]
    [InlineData(" withspace.txt")]
    [InlineData("a?b.txt")]
    public async Task WriteAtomicAsync_InvalidKey_Throws(string key)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.WriteAtomicAsync(_tempRoot, "tenant1", key, Encoding.UTF8.GetBytes("x")));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("./")]
    [InlineData("/static")]
    [InlineData("static/")]
    [InlineData("sta tic")]
    [InlineData("sta/../tic")]
    [InlineData("static//images")]
    public async Task WriteAtomicAsync_InvalidSubFolder_Throws(string subFolder)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.WriteAtomicAsync(_tempRoot, "tenant1", "a/b.txt", Encoding.UTF8.GetBytes("x"), subFolder));
    }
    
     [Fact]
    public async Task WriteAtomicAsync_EmptyContent_CreatesZeroLengthWithKnownEtag()
    {
        var tenant = "t1";
        var key = "folder/empty.txt";
        var empty = Array.Empty<byte>();

        var (fullPath, etag, fi) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, empty);

        Assert.True(File.Exists(fullPath));
        Assert.Equal(0, fi.Length);
        // SHA-256 von leerem Inhalt
        Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", etag);
    }

    [Fact]
    public async Task WriteAtomicAsync_TargetIsReadOnly_ThrowsIoOrUnauthorized()
    {
        var tenant = "t1";
        var key = "folder/readonly.txt";
        var path = IoUtilities.BuildSafeFullPath(_tempRoot, tenant, key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "seed", Encoding.UTF8);

        var attrs = File.GetAttributes(path);
        File.SetAttributes(path, attrs | FileAttributes.ReadOnly);

        try
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
                await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, Encoding.UTF8.GetBytes("new")));

            Assert.True(ex is IOException || ex is UnauthorizedAccessException);
        }
        finally
        {
            // Cleanup
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }

    [Fact]
    public async Task WriteAtomicAsync_VeryLongPath_StaysUnderRootOrThrows()
    {
        var tenant = "tenant1";
        var seg = new string('x', 150);
        var key = $"{seg}/{seg}/{seg}.txt";

        try
        {
            var (fullPath, _, fi) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, Encoding.UTF8.GetBytes("data"));
            var rootWithSep = Path.GetFullPath(_tempRoot) + Path.DirectorySeparatorChar;
            Assert.StartsWith(rootWithSep, fullPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(fi.Exists);
        }
        catch (PathTooLongException)
        {
            // Plattformabhängig akzeptabel; Test lässt beide Ergebnisse zu.
            Assert.True(true);
        }
        catch (IOException ex) when (ex.Message.Contains("too long", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task ExistsSafe_ReturnsTrueWhenFileExists()
    {
        var tenant = "t1";
        var key = "exists/file.txt";
        var (path, _, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, Encoding.UTF8.GetBytes("data"));
        Assert.True(File.Exists(path)); // sanity

        var exists = IoUtilities.ExistsSafe(_tempRoot, tenant, key);
        Assert.True(exists);
    }

    [Fact]
    public void ExistsSafe_ReturnsFalseWhenFileMissing()
    {
        var exists = IoUtilities.ExistsSafe(_tempRoot, "t1", "missing/none.txt");
        Assert.False(exists);
    }

    [Fact]
    public void ExistsSafe_WithSubFolder_Works()
    {
        var tenant = "t1";
        var key = "a/b.txt";
        var path = IoUtilities.BuildSafeFullPath(_tempRoot, tenant, key, "static");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x", Encoding.UTF8);

        var exists = IoUtilities.ExistsSafe(_tempRoot, tenant, key, "static");
        Assert.True(exists);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a//b.txt")]
    [InlineData("/abs.txt")]
    public void ExistsSafe_InvalidKey_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.ExistsSafe(_tempRoot, "tenant1", key));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("/static")]
    [InlineData("static//images")]
    public void ExistsSafe_InvalidSubFolder_Throws(string subFolder)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.ExistsSafe(_tempRoot, "tenant1", "a/b.txt", subFolder));
    }

    [Theory]
    [InlineData(null, "t", "k", "baseRootFull")]
    [InlineData("", "t", "k", "baseRootFull")]
    [InlineData(" ", "t", "k", "baseRootFull")]
    [InlineData("root", null, "k", "tenantIdString")]
    [InlineData("root", "", "k", "tenantIdString")]
    [InlineData("root", " ", "k", "tenantIdString")]
    [InlineData("root", "t", null, "key")]
    [InlineData("root", "t", " ", "key")]
    public void ExistsSafe_NullOrWhitespace_Throws(string? baseRoot, string? tenant, string? key, string expectedParam)
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            IoUtilities.ExistsSafe(baseRoot!, tenant!, key!));
        Assert.Equal(expectedParam, ex.ParamName);
    }
}