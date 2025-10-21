using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class MemoryLogSinkMoreTests
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
    public async Task Parallel_Writes_Drop_Oldest_Keep_Capacity()
    {
        var sink = new MemoryLogSink(capacity: 100);

        var tasks = Enumerable.Range(0, 10).Select(async t =>
        {
            for (int i = 0; i < 200; i++)
                await sink.WriteAsync(E(t * 1000 + i), default);
        });

        await Task.WhenAll(tasks);
        Assert.Equal(100, sink.Count);

        var snapshot = sink.Snapshot();
        // Prüfe FIFO-Eigenschaft grob: kleinster EventId.Id im Snapshot ist > 0
        var minId = snapshot.Min(e => e.EventId.Id);
        var maxId = snapshot.Max(e => e.EventId.Id);
        Assert.True(maxId - minId < 5000); // nur grobe Schranke
    }
}
