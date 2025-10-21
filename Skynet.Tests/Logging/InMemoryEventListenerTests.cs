using System.Collections.Concurrent;
using System.Text;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class InMemoryListenerTests
{
    [Fact]
    public async Task Bind_Accept_Read_Close_Workflow()
    {
        // Arrange
        var listener = new InMemoryEventListener();
        var frames = new[]
        {
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("frame1")),
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("frame2"))
        };
        var channel = new InMemoryEventChannel(frames);

        await listener.BindAsync(CancellationToken.None);
        listener.EnqueueChannel(channel);

        // Act
        var accepted = await listener.AcceptAsync(CancellationToken.None);
        var r1 = await accepted.ReadAsync(CancellationToken.None);
        var r2 = await accepted.ReadAsync(CancellationToken.None);
        var r3 = await accepted.ReadAsync(CancellationToken.None); // sollte null sein (Ende)

        await accepted.CloseAsync(CancellationToken.None);
        await listener.CloseAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(accepted);
        Assert.Equal("frame1", Encoding.UTF8.GetString(r1!.Value.Span));
        Assert.Equal("frame2", Encoding.UTF8.GetString(r2!.Value.Span));
        Assert.Null(r3);
    }

    [Fact]
    public async Task Accept_Without_Pending_Channels_Throws()
    {
        var listener = new InMemoryEventListener();
        await listener.BindAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await listener.AcceptAsync(CancellationToken.None);
        });
        Assert.Contains("No pending channels", ex.Message);
    }

    [Fact]
    public async Task Accept_When_Closed_Throws_OperationCanceled()
    {
        var listener = new InMemoryEventListener();
        await listener.BindAsync(CancellationToken.None);
        await listener.CloseAsync(CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await listener.AcceptAsync(CancellationToken.None);
        });
    }

    [Fact]
    public async Task Read_Returns_Null_After_Close()
    {
        var frames = new[]
        {
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("only"))
        };
        var ch = new InMemoryEventChannel(frames);

        // Erstes Read liefert Frame
        var f1 = await ch.ReadAsync(CancellationToken.None);
        Assert.NotNull(f1);

        // Close -> weitere Reads liefern null
        await ch.CloseAsync(CancellationToken.None);
        var f2 = await ch.ReadAsync(CancellationToken.None);
        Assert.Null(f2);
    }

    [Fact]
    public async Task Channel_ReadEmpty_ReturnsNull_Immediately()
    {
        var ch = new InMemoryEventChannel(Array.Empty<ReadOnlyMemory<byte>>());
        var frame = await ch.ReadAsync(CancellationToken.None);
        Assert.Null(frame);
        await ch.DisposeAsync();
    }

    [Fact]
    public async Task Channel_ParallelReads_Are_ThreadSafe_And_Finite()
    {
        var frames = Enumerable.Range(0, 100)
            .Select(i => new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes($"f{i}")))
            .ToArray();
        var ch = new InMemoryEventChannel(frames);

        var results = new ConcurrentBag<string?>();
        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            while (true)
            {
                var f = await ch.ReadAsync(CancellationToken.None);
                if (f is null) break;
                results.Add(Encoding.UTF8.GetString(f.Value.Span));
            }
        });
        await Task.WhenAll(tasks);

        Assert.Equal(100, results.Count);
        Assert.Equal(100, results.Distinct().Count());
        await ch.DisposeAsync();
    }

    [Fact]
    public async Task Channel_Dispose_BeforeEnd_MakesFurtherReadsNull()
    {
        var frames = new[]
        {
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("x"))
        };
        var ch = new InMemoryEventChannel(frames);

        await ch.DisposeAsync();
        var f = await ch.ReadAsync(CancellationToken.None);
        Assert.Null(f);
    }

    [Fact]
    public async Task Listener_Bind_After_Close_Throws()
    {
        var l = new InMemoryEventListener();
        await l.CloseAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await l.BindAsync(CancellationToken.None);
        });
    }

    [Fact]
    public async Task Listener_Accept_With_Canceled_Token_Throws()
    {
        var l = new InMemoryEventListener();
        await l.BindAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await l.AcceptAsync(cts.Token);
        });
    }

    [Fact]
    public async Task Listener_Accept_Fifo_For_Multiple_Channels()
    {
        var l = new InMemoryEventListener();
        await l.BindAsync(CancellationToken.None);

        var ch1 = new InMemoryEventChannel(new[]
        {
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("c1f1")),
        });
        var ch2 = new InMemoryEventChannel(new[]
        {
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("c2f1")),
        });

        l.EnqueueChannel(ch1);
        l.EnqueueChannel(ch2);

        var a1 = await l.AcceptAsync(CancellationToken.None);
        var a2 = await l.AcceptAsync(CancellationToken.None);

        var r1 = await a1.ReadAsync(CancellationToken.None);
        var r2 = await a2.ReadAsync(CancellationToken.None);

        Assert.Equal("c1f1", Encoding.UTF8.GetString(r1!.Value.Span));
        Assert.Equal("c2f1", Encoding.UTF8.GetString(r2!.Value.Span));
    }

    [Fact]
    public async Task EndToEnd_TwoChannels_AllFramesRead_ThenClosed()
    {
        var l = new InMemoryEventListener();
        await l.BindAsync(CancellationToken.None);

        var ch1Frames = new[]
        {
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("a1")),
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("a2")),
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("a3")),
        };
        var ch2Frames = new[]
        {
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("b1")),
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("b2")),
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("b3")),
        };

        var ch1 = new InMemoryEventChannel(ch1Frames);
        var ch2 = new InMemoryEventChannel(ch2Frames);

        l.EnqueueChannel(ch1);
        l.EnqueueChannel(ch2);

        var a1 = await l.AcceptAsync(CancellationToken.None);
        var a2 = await l.AcceptAsync(CancellationToken.None);

        async Task<List<string>> ReadAllAsync(IEventChannel ch)
        {
            var list = new List<string>();
            while (true)
            {
                var f = await ch.ReadAsync(CancellationToken.None);
                if (f is null) break;
                list.Add(Encoding.UTF8.GetString(f.Value.Span));
            }
            return list;
        }

        var frames1 = await ReadAllAsync(a1);
        var frames2 = await ReadAllAsync(a2);

        await a1.CloseAsync(CancellationToken.None);
        await a2.CloseAsync(CancellationToken.None);
        await l.CloseAsync(CancellationToken.None);

        Assert.Equal(new[] { "a1", "a2", "a3" }, frames1);
        Assert.Equal(new[] { "b1", "b2", "b3" }, frames2);
    }
}
