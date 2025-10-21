using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class MemoryLogSinkTests
{
    private static MutableLogEvent E(int i) => new()
    {
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(0),
        Level = LogLevel.Information,
        EventId = new EventId(i),
        GlobalEventId = i.ToString(),
        State = Array.Empty<KeyValuePair<string, object?>>()
    };

    [Fact]
    public async Task Stores_Upto_Capacity_Drops_Oldest()
    {
        var sink = new MemoryLogSink(capacity: 3);
        await sink.StartAsync(default);

        await sink.WriteAsync(E(1), default);
        await sink.WriteAsync(E(2), default);
        await sink.WriteAsync(E(3), default);
        await sink.WriteAsync(E(4), default); // drop 1

        var snapshot = sink.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(2, snapshot[0].EventId.Id);
        Assert.Equal(3, snapshot[1].EventId.Id);
        Assert.Equal(4, snapshot[2].EventId.Id);
    }

    [Fact]
    public async Task Clear_Empties_Buffer()
    {
        var sink = new MemoryLogSink(2);
        await sink.WriteAsync(E(1), default);
        sink.Clear();
        Assert.Equal(0, sink.Count);
        Assert.Empty(sink.Snapshot());
    }

    [Fact]
    public async Task Start_Flush_Dispose_NoThrow()
    {
        var sink = new MemoryLogSink(2);
        await sink.StartAsync(default);
        await sink.FlushAsync(default);
        await sink.DisposeAsync();
    }

    [Fact]
    public async Task Capacity_Property_Reported()
    {
        var sink = new MemoryLogSink(5);
        await sink.WriteAsync(E(1), default);
        Assert.Equal(5, sink.Capacity);
        Assert.Equal(1, sink.Count);
    }
}
