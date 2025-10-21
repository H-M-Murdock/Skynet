using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class SingleSinkRouterTests
{
    private static MutableLogEvent E() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogLevel.Information,
        EventId = new EventId(1),
        GlobalEventId = "gid",
        State = Array.Empty<KeyValuePair<string, object?>>()
    };

    [Fact]
    public void Resolve_Returns_Same_Sink()
    {
        var mem = new MemoryLogSink(10);
        var router = new SingleSinkRouter(mem);

        var s1 = router.Resolve(E());
        var s2 = router.Resolve(E());

        Assert.Same(mem, s1);
        Assert.Same(s1, s2);
    }

    [Fact]
    public async Task Dispose_Disposes_Inner_Sink()
    {
        var mem = new MemoryLogSink(5);
        var router = new SingleSinkRouter(mem);

        await router.DisposeAsync(); // sollte nicht werfen
    }
}
