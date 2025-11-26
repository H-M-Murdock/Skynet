using System.Text;
using Skynet.Core;
using Xunit;

namespace Skynet.Tests.IOUtilities;

public class IoUtilitiesTests: IDisposable
{
    private readonly string _tempRoot;

    public IoUtilitiesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "IoUtilitiesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Falls Handles offen sind, bestenfalls ignorieren – OS-Temp wird periodisch bereinigt.
        }
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

            // Zwei konkurrierende Writes starten
            var t1 = IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, v1);
            var t2 = IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, v2);

            // Warten, bis beide abgeschlossen sind. Eine IOException ist möglich und wird erwartet.
            try
            {
                await Task.WhenAll(t1, t2);
            }
            catch (IOException)
            {
                // Erwartetes Ergebnis bei einer Race Condition. Mindestens ein Task sollte 
                // dennoch erfolgreich sein oder bereits erfolgreich gewesen sein.
            }

            // Überprüfen des finalen Zustands auf der Festplatte
            var path = IoUtilities.BuildSafeFullPath(_tempRoot, tenant, key);
            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path, Encoding.UTF8);

            // Der Inhalt muss entweder "first" oder "second" sein.
            Assert.True(content is "first" or "second", "File content is not one of the expected values.");

            // Identifiziere den "gewinnenden" Task basierend auf dem finalen Inhalt.
            var winner = content == "first" ? t1 : t2;
            var loser = content == "first" ? t2 : t1;

            // Der Gewinner-Task MUSS erfolgreich abgeschlossen worden sein.
            Assert.True(winner.IsCompletedSuccessfully, $"The winning task (content: '{content}') should have completed successfully.");

            // Der ETag des Gewinners muss mit dem Hash des finalen Inhalts übereinstimmen.
            var finalEtag = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content)));
            var winnerResult = await winner;
            Assert.Equal(finalEtag, winnerResult.etag);
            
            // Der Verlierer-Task kann entweder erfolgreich gewesen (und überschrieben worden)
            // oder mit einer IOException fehlgeschlagen sein. Beides ist okay.
            Assert.True(loser.IsCompletedSuccessfully || loser.IsFaulted, "The loser task should either have succeeded or faulted.");
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
    
    
    [Fact]
    public async Task DeleteSafeAsync_DeletesExistingFile_ReturnsTrue()
    {
        var tenant = "t1";
        var key = "del/existing.txt";
        var (path, _, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, Encoding.UTF8.GetBytes("x"));
        Assert.True(File.Exists(path));

        var deleted = await IoUtilities.DeleteSafeAsync(_tempRoot, tenant, key);
        Assert.True(deleted);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteSafeAsync_MissingFile_ReturnsFalse()
    {
        var deleted = await IoUtilities.DeleteSafeAsync(_tempRoot, "t1", "del/missing.txt");
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteSafeAsync_WithSubFolder_Works()
    {
        var tenant = "t1";
        var key = "a/b.txt";
        var (path, _, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, key, Encoding.UTF8.GetBytes("x"), "static");
        Assert.True(File.Exists(path));

        var deleted = await IoUtilities.DeleteSafeAsync(_tempRoot, tenant, key, "static");
        Assert.True(deleted);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteSafeAsync_RespectsCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await IoUtilities.DeleteSafeAsync(_tempRoot, "t", "k.txt", ct: cts.Token));
    }

    [Theory]
    [InlineData(null, "t", "k.txt", "baseRootFull")]
    [InlineData("", "t", "k.txt", "baseRootFull")]
    [InlineData(" ", "t", "k.txt", "baseRootFull")]
    [InlineData("root", null, "k.txt", "tenantIdString")]
    [InlineData("root", "", "k.txt", "tenantIdString")]
    [InlineData("root", " ", "k.txt", "tenantIdString")]
    [InlineData("root", "t", null, "key")]
    [InlineData("root", "t", " ", "key")]
    public async Task DeleteSafeAsync_NullOrWhitespace_Throws(string? baseRoot, string? tenant, string? key, string expectedParam)
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await IoUtilities.DeleteSafeAsync(baseRoot!, tenant!, key!));
        Assert.Equal(expectedParam, ex.ParamName);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a//b.txt")]
    [InlineData("/abs.txt")]
    public async Task DeleteSafeAsync_InvalidKey_Throws(string key)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.DeleteSafeAsync(_tempRoot, "tenant1", key));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("/static")]
    [InlineData("static//images")]
    public async Task DeleteSafeAsync_InvalidSubFolder_Throws(string subFolder)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.DeleteSafeAsync(_tempRoot, "tenant1", "a/b.txt", subFolder));
    }
    
     [Fact]
    public void DirectoryExistsSafe_ReturnsFalse_WhenMissing()
    {
        var exists = IoUtilities.DirectoryExistsSafe(_tempRoot, "tenant1", "folders/none", subFolder: "static");
        Assert.False(exists);
    }

    [Fact]
    public void DirectoryExistsSafe_ReturnsTrue_WhenCreated()
    {
        var path = IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", "folders/sub", "static");
        Directory.CreateDirectory(path);
        Assert.True(Directory.Exists(path));

        var exists = IoUtilities.DirectoryExistsSafe(_tempRoot, "tenant1", "folders/sub", "static");
        Assert.True(exists);
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
    public void DirectoryExistsSafe_NullOrWhitespace_Throws(string? baseRoot, string? tenant, string? key, string expectedParam)
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            IoUtilities.DirectoryExistsSafe(baseRoot!, tenant!, key!));
        Assert.Equal(expectedParam, ex.ParamName);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a//b")]
    [InlineData("/abs")]
    public void DirectoryExistsSafe_InvalidKey_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.DirectoryExistsSafe(_tempRoot, "tenant1", key));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("/static")]
    [InlineData("static//images")]
    public void DirectoryExistsSafe_InvalidSubFolder_Throws(string subFolder)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IoUtilities.DirectoryExistsSafe(_tempRoot, "tenant1", "a/b", subFolder));
    }

    [Fact]
    public async Task DirectoryDeleteSafeAsync_Deletes_WhenExists()
    {
        var path = IoUtilities.BuildSafeFullPath(_tempRoot, "tenant1", "to/delete/dir", "static");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "file.txt"), "x");

        var deleted = await IoUtilities.DirectoryDeleteSafeAsync(_tempRoot, "tenant1", "to/delete/dir", "static");
        Assert.True(deleted);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task DirectoryDeleteSafeAsync_ReturnsFalse_WhenMissing()
    {
        var deleted = await IoUtilities.DirectoryDeleteSafeAsync(_tempRoot, "tenant1", "not/exist", "static");
        Assert.False(deleted);
    }

    [Fact]
    public async Task DirectoryDeleteSafeAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await IoUtilities.DirectoryDeleteSafeAsync(_tempRoot, "tenant1", "a/b", ct: cts.Token));
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
    public async Task DirectoryDeleteSafeAsync_NullOrWhitespace_Throws(string? baseRoot, string? tenant, string? key, string expectedParam)
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await IoUtilities.DirectoryDeleteSafeAsync(baseRoot!, tenant!, key!));
        Assert.Equal(expectedParam, ex.ParamName);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a//b")]
    [InlineData("/abs")]
    public async Task DirectoryDeleteSafeAsync_InvalidKey_Throws(string key)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.DirectoryDeleteSafeAsync(_tempRoot, "tenant1", key));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("/static")]
    [InlineData("static//images")]
    public async Task DirectoryDeleteSafeAsync_InvalidSubFolder_Throws(string subFolder)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.DirectoryDeleteSafeAsync(_tempRoot, "tenant1", "a/b", subFolder));
    }
    
    [Fact]
    public async Task CopySafeAsync_CopiesFile_AndReturnsDestInfo()
    {
        var tenant = "t1";
        var srcKey = "copy/src.txt";
        var dstKey = "copy/dst.txt";

        var (_, srcEtag, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, srcKey, Encoding.UTF8.GetBytes("copy-data"));

        var (dstPath, dstInfo) = await IoUtilities.CopySafeAsync(_tempRoot, tenant, srcKey, dstKey);

        Assert.True(File.Exists(dstPath));
        Assert.Equal(dstPath, dstInfo.FullName);
        var (_, checkEtag, _) = await IoUtilities.OpenReadWithHashAsync(dstPath);
        Assert.Equal(srcEtag, checkEtag);
    }

    [Fact]
    public async Task CopySafeAsync_WithSubFolder_Works()
    {
        var tenant = "t1";
        var srcKey = "copy/sf/src.txt";
        var dstKey = "copy/sf/dst.txt";

        await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, srcKey, Encoding.UTF8.GetBytes("x"), subFolder: "static");
        var (dstPath, _) = await IoUtilities.CopySafeAsync(_tempRoot, tenant, srcKey, dstKey, subFolder: "static");

        Assert.EndsWith(Path.Combine(tenant, "static", "copy", "sf", "dst.txt"), dstPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CopySafeAsync_MissingSource_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await IoUtilities.CopySafeAsync(_tempRoot, "t1", "nope/src.txt", "copy/dst.txt"));
    }

    [Fact]
    public async Task CopySafeAsync_RespectsCancellation()
    {
        var tenant = "t1";
        var srcKey = "copy/cancel/src.bin";
        var dstKey = "copy/cancel/dst.bin";
        await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, srcKey, new byte[1024 * 1024]);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await IoUtilities.CopySafeAsync(_tempRoot, tenant, srcKey, dstKey, ct: cts.Token));
    }

    [Fact]
    public async Task MoveSafeAsync_MovesFile_AndRemovesSource()
    {
        var tenant = "t1";
        var srcKey = "move/src.txt";
        var dstKey = "move/dst.txt";

        var (srcPath, srcEtag, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, srcKey, Encoding.UTF8.GetBytes("move-data"));
        Assert.True(File.Exists(srcPath));

        var (moved, dstPath, dstInfo) = await IoUtilities.MoveSafeAsync(_tempRoot, tenant, srcKey, dstKey);
        Assert.True(moved);

        Assert.False(File.Exists(srcPath));
        Assert.True(File.Exists(dstPath));

        var (_, checkEtag, _) = await IoUtilities.OpenReadWithHashAsync(dstPath);
        Assert.Equal(srcEtag, checkEtag);
        Assert.Equal(dstPath, dstInfo.FullName);
    }

    [Fact]
    public async Task MoveSafeAsync_WithSubFolder_Works()
    {
        var tenant = "t1";
        var srcKey = "move/sf/src.txt";
        var dstKey = "move/sf/dst.txt";

        var (srcPath, _, _) = await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, srcKey, Encoding.UTF8.GetBytes("y"), subFolder: "static");
        Assert.True(File.Exists(srcPath));

        var (moved, dstPath, _) = await IoUtilities.MoveSafeAsync(_tempRoot, tenant, srcKey, dstKey, subFolder: "static");
        Assert.True(moved);

        Assert.False(File.Exists(srcPath));
        Assert.EndsWith(Path.Combine(tenant, "static", "move", "sf", "dst.txt"), dstPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveSafeAsync_MissingSource_ReturnsFalse()
    {
        var moved = await IoUtilities.MoveSafeAsync(_tempRoot, "t1", "move/nope.txt", "move/dst.txt");
        Assert.False(moved.moved);
    }

    [Fact]
    public async Task MoveSafeAsync_RespectsCancellation()
    {
        var tenant = "t1";
        var srcKey = "move/cancel/src.bin";
        var dstKey = "move/cancel/dst.bin";
        await IoUtilities.WriteAtomicAsync(_tempRoot, tenant, srcKey, new byte[256 * 1024]);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await IoUtilities.MoveSafeAsync(_tempRoot, tenant, srcKey, dstKey, ct: cts.Token));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("/abs.txt")]
    [InlineData("a//b.txt")]
    public async Task CopySafeAsync_InvalidKey_Throws(string badKey)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.CopySafeAsync(_tempRoot, "tenant1", badKey, "dst.txt"));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("/abs.txt")]
    [InlineData("a//b.txt")]
    public async Task MoveSafeAsync_InvalidKey_Throws(string badKey)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await IoUtilities.MoveSafeAsync(_tempRoot, "tenant1", badKey, "dst.txt"));
    }
    
}