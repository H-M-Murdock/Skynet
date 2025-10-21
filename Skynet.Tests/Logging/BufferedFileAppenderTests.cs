using System.Text;
using Skynet.Core;

namespace Skynet.Tests.Logging;

public sealed class BufferedFileAppenderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bfw_" + Guid.NewGuid().ToString("n"));
    private readonly string _tenant = "tlogs";

    public BufferedFileAppenderTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Append_And_Flush_Writes_To_File()
    {
        var key = "a/b.txt";
        var full = IoUtilities.BuildSafeFullPath(_root, _tenant, key);

        var app = new BufferedFileAppender(writeThrough: false);
        await app.EnsureOpenedForAsync(full, CancellationToken.None);

        app.Append("line1\n");
        app.Append("line2\n");
        await app.FlushAsync(CancellationToken.None);
        await app.DisposeAsync(); // Stream schließen

        Assert.True(File.Exists(full));
        var text = await File.ReadAllTextAsync(full, Encoding.UTF8);
        Assert.Contains("line1", text);
        Assert.Contains("line2", text);
    }

    [Fact]
    public async Task Rotation_On_RotateTo_Creates_Rotated_File()
    {
        var key = "rot/test.log";
        var full = IoUtilities.BuildSafeFullPath(_root, _tenant, key);

        var app = new BufferedFileAppender(writeThrough: true);
        await app.EnsureOpenedForAsync(full, CancellationToken.None);

        app.Append("1234567890\n"); // 11
        await app.FlushAsync(CancellationToken.None);

        // Erzwinge Rotation auf denselben Basisnamen (Appender hängt .1 an)
        await app.RotateToAsync(full, CancellationToken.None);

        app.Append("ABCDEFGHIJ\n");
        await app.FlushAsync(CancellationToken.None);

        Assert.True(File.Exists(full));
        var rotated = full + ".1";
        Assert.True(File.Exists(rotated));

        await app.DisposeAsync();
    }

    [Fact]
    public async Task Multiple_Flushes_Append_To_Same_File()
    {
        var key = "append/same.log";
        var full = IoUtilities.BuildSafeFullPath(_root, _tenant, key);

        var app = new BufferedFileAppender(writeThrough: false);
        await app.EnsureOpenedForAsync(full, CancellationToken.None);

        app.Append("A\n");
        await app.FlushAsync(CancellationToken.None);
        app.Append("B\n");
        await app.FlushAsync(CancellationToken.None);
        await app.DisposeAsync(); // schließen vor dem Lesen

        var lines = await File.ReadAllLinesAsync(full, Encoding.UTF8);
        Assert.Contains("A", lines[0]);
        Assert.Contains("B", lines[1]);
    }

    [Fact]
    public async Task Dispose_Closes_Stream()
    {
        var key = "dispose/close.log";
        var full = IoUtilities.BuildSafeFullPath(_root, _tenant, key);

        var app = new BufferedFileAppender();
        await app.EnsureOpenedForAsync(full, CancellationToken.None);
        app.Append("x\n");
        await app.FlushAsync(CancellationToken.None);
        await app.DisposeAsync();

        // erneutes Anhängen über neuen Appender
        var app2 = new BufferedFileAppender();
        await app2.EnsureOpenedForAsync(full, CancellationToken.None);
        app2.Append("y\n");
        await app2.FlushAsync(CancellationToken.None);
        await app2.DisposeAsync();

        var all = await File.ReadAllTextAsync(full, Encoding.UTF8);
        Assert.Contains("x", all);
        Assert.Contains("y", all);
    }
}
