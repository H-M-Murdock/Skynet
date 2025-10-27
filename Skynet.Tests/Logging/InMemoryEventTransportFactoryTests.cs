using System.Text;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class InMemoryEventTransportFactoryTests
{
    // --- Deterministischer Test-Encoder ---
    private sealed class TestEncoder : ILogEventEncoder
    {
        public string ContentType => "application/x-test";
        public string Version => "1.0";

        public ReadOnlyMemory<byte> Encode(ILogEvent evt)
            => Encoding.UTF8.GetBytes($"id:{evt.EventId.Id}");

        public bool TryDecode(ReadOnlySpan<byte> payload, out ILogEvent? evt)
        {
            var s = Encoding.UTF8.GetString(payload);
            if (!s.StartsWith("id:", StringComparison.Ordinal) ||
                !int.TryParse(s.AsSpan(3), out var n))
            {
                evt = null; return false;
            }

            evt = new MutableLogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = LogLevel.Information,
                EventId = new EventId(n, "E"),
                GlobalEventId = n.ToString("n"),
                State = new List<KeyValuePair<string, object?>>()
            };
            return true;
        }
    }

    // --- Test-Hilfen ---
    private sealed class CapturingSink : ILogSink
    {
        public readonly List<ILogEvent> Events = new();
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task WriteAsync(ILogEvent evt, CancellationToken ct)
        { lock (Events) Events.Add(evt); return Task.CompletedTask; }
        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SingleRouter : ILogRouter
    {
        private readonly ILogSink _sink;
        public SingleRouter(ILogSink sink) => _sink = sink;
        public ILogSink Resolve(ILogEvent evt) => _sink;
    }

    private static MutableLogEvent E(int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogLevel.Information,
        EventId = new EventId(id, "E"),
        GlobalEventId = id.ToString("n"),
        State = new List<KeyValuePair<string, object?>>()
    };

    // --- Tests ---

    [Fact]
    public async Task Create_Returns_Working_Transport_That_Reaches_Server()
    {
        var listener     = new InMemoryEventListener();
        var encoder      = new TestEncoder();
        var sink         = new CapturingSink();
        var router       = new SingleRouter(sink);
        var materializer = new DefaultLogEventMaterializer();

        var server = new LoggingServer(
            listener, encoder, router, materializer,
            new LoggingServerOptions
            {
                WorkerCount = 1,
                QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                PeriodicFlushInterval = TimeSpan.Zero,
                AcceptBackoff = TimeSpan.FromMilliseconds(5)
            });
        await server.StartAsync(CancellationToken.None);

        var factory   = new InMemoryEventTransportFactory(listener, defaultCapacity: 8);
        var transport = factory.Create();

        await transport.ConnectAsync(CancellationToken.None);
        Assert.Equal(TransportState.Connected, transport.State);

        Assert.True(await transport.TrySendAsync(encoder.Encode(E(1)), CancellationToken.None));
        await transport.FlushAsync(CancellationToken.None);

        await server.StopAsync(CancellationToken.None);

        Assert.Contains(1, sink.Events.Select(e => e.EventId.Id));
    }

    [Fact]
    public async Task Factory_Can_Create_Multiple_Independent_Transports()
    {
        var listener     = new InMemoryEventListener();
        var encoder      = new TestEncoder();
        var sink         = new CapturingSink();
        var router       = new SingleRouter(sink);
        var materializer = new DefaultLogEventMaterializer();

        var server = new LoggingServer(
            listener, encoder, router, materializer,
            new LoggingServerOptions
            {
                WorkerCount = 1,
                QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                PeriodicFlushInterval = TimeSpan.Zero
            });
        await server.StartAsync(CancellationToken.None);

        var factory = new InMemoryEventTransportFactory(listener, defaultCapacity: 4);
        var t1 = factory.Create();
        var t2 = factory.Create();

        await t1.ConnectAsync(CancellationToken.None);
        await t2.ConnectAsync(CancellationToken.None);

        Assert.True(await t1.TrySendAsync(encoder.Encode(E(10)), CancellationToken.None));
        Assert.True(await t2.TrySendAsync(encoder.Encode(E(20)), CancellationToken.None));

        await t1.FlushAsync(CancellationToken.None);
        await t2.FlushAsync(CancellationToken.None);

        await server.StopAsync(CancellationToken.None);

        var ids = sink.Events.Select(e => e.EventId.Id).ToHashSet();
        Assert.Contains(10, ids);
        Assert.Contains(20, ids);
    }

    [Fact]
    public async Task Factory_Respects_DefaultCapacity_For_Backpressure()
    {
        // Kein Server: niemand drain’t -> wir sehen reines Backpressure am Transport
        var listener = new InMemoryEventListener();
        await listener.BindAsync(CancellationToken.None);

        var factory   = new InMemoryEventTransportFactory(listener, defaultCapacity: 2);
        var transport = factory.Create();

        await transport.ConnectAsync(CancellationToken.None);

        var payload = Encoding.UTF8.GetBytes("id:1");
        Assert.True(await transport.TrySendAsync(payload, CancellationToken.None));
        Assert.True(await transport.TrySendAsync(payload, CancellationToken.None));
        var third = await transport.TrySendAsync(payload, CancellationToken.None);

        Assert.False(third); // Kapazität erreicht
        await transport.CloseAsync(CancellationToken.None);
        Assert.Equal(TransportState.Closed, transport.State);
    }
}
