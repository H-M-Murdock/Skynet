using System.Text;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class InMemoryEventTransportTests
{
    // --- Test-Hilfen ---
    private sealed class CapturingSink : ILogSink
    {
        public readonly List<ILogEvent> Events = new();
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task WriteAsync(ILogEvent evt, CancellationToken ct)
        {
            lock (Events) Events.Add(evt);
            return Task.CompletedTask;
        }
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

    private static ReadOnlyMemory<byte> Enc(ILogEventEncoder enc, ILogEvent evt) => enc.Encode(evt);

    // --- Tests ---

    [Fact]
    public async Task Connect_And_Send_Reaches_Server_Sink()
    {
        // Arrange (Server nach aktuellem Modell mit bounded Dispatch-Queue + Worker)
        var listener = new InMemoryEventListener();
        var encoder  = new NdjsonLogEventEncoder();
        var sink     = new CapturingSink();
        var router   = new SingleRouter(sink);

        var serverOptions = new LoggingServerOptions
        {
            // schnelleres Feedback im Test
            AcceptBackoff = TimeSpan.FromMilliseconds(10),
            PeriodicFlushInterval = TimeSpan.Zero, // wir flushen am Ende manuell
            QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
            WorkerCount = 1
        };
        var server = new LoggingServer(listener, encoder, router, serverOptions);
        await server.StartAsync(CancellationToken.None);

        var transport = new InMemoryEventTransport(listener, capacity: 8);

        // Act
        await transport.ConnectAsync(CancellationToken.None);
        Assert.Equal(TransportState.Connected, transport.State);

        var p1 = Enc(encoder, E(1));
        var p2 = Enc(encoder, E(2));
        Assert.True(await transport.TrySendAsync(p1, CancellationToken.None));
        Assert.True(await transport.TrySendAsync(p2, CancellationToken.None));

        await transport.FlushAsync(CancellationToken.None); // wartet bis transportseitiger Buffer leer ist
        await server.StopAsync(CancellationToken.None);     // Server drain+flush gemäß Stop-Logik

        // Assert
        var ids = sink.Events.Select(e => e.EventId.Id).ToHashSet();
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public async Task Backpressure_ReturnsFalse_When_Transport_Buffer_Full_And_No_Consumer()
    {
        // Arrange: kein laufender Server -> niemand akzeptiert/liest; nur Transport prüfen
        var listener  = new InMemoryEventListener();
        await listener.BindAsync(CancellationToken.None);
        var transport = new InMemoryEventTransport(listener, capacity: 2);

        await transport.ConnectAsync(CancellationToken.None);

        var payload = Encoding.UTF8.GetBytes("{x:1}");

        // Act: Fülle den rein transport-internen Buffer (bounded, non-blocking)
        Assert.True(await transport.TrySendAsync(payload, CancellationToken.None));
        Assert.True(await transport.TrySendAsync(payload, CancellationToken.None));
        var third = await transport.TrySendAsync(payload, CancellationToken.None); // sollte an Kapazitätsgrenze scheitern

        // Assert
        Assert.False(third);
        await transport.CloseAsync(CancellationToken.None);
        Assert.Equal(TransportState.Closed, transport.State);
    }

    [Fact]
    public async Task Flush_Waits_Until_Server_Drains_Dispatch_Queue()
    {
        // Arrange: vollständiger E2E-Durchfluss mit Warten bei voller Server-Queue
        var listener = new InMemoryEventListener();
        var encoder  = new NdjsonLogEventEncoder();
        var sink     = new CapturingSink();
        var router   = new SingleRouter(sink);

        // Server wartet statt zu droppen, damit Flush deterministisch leer laufen kann
        var server = new LoggingServer(listener, encoder, router, new LoggingServerOptions
        {
            QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
            WorkerCount = 1,
            PeriodicFlushInterval = TimeSpan.Zero,
            AcceptBackoff = TimeSpan.FromMilliseconds(5)
        });
        await server.StartAsync(CancellationToken.None);

        var transport = new InMemoryEventTransport(listener, capacity: 4);
        await transport.ConnectAsync(CancellationToken.None);

        // Act
        var frames = Enumerable.Range(0, 4).Select(i => Enc(encoder, E(i))).ToArray();
        foreach (var f in frames)
            Assert.True(await transport.TrySendAsync(f, CancellationToken.None));

        // Wartet, bis transportseitig alles raus ist (Server baut zentralen Dispatch ab)
        await transport.FlushAsync(CancellationToken.None);

        await server.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(4, sink.Events.Count);
        var ids = sink.Events.Select(e => e.EventId.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, ids);
    }

    [Fact]
    public async Task Close_Signals_Eof_To_Server_And_Transitions_State()
    {
        var listener = new InMemoryEventListener();
        var encoder  = new NdjsonLogEventEncoder();
        var sink     = new CapturingSink();
        var router   = new SingleRouter(sink);

        var server = new LoggingServer(listener, encoder, router, new LoggingServerOptions
        {
            QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
            WorkerCount = 1,
            PeriodicFlushInterval = TimeSpan.Zero
        });
        await server.StartAsync(CancellationToken.None);

        var transport = new InMemoryEventTransport(listener, capacity: 2);
        await transport.ConnectAsync(CancellationToken.None);

        // ein Event
        Assert.True(await transport.TrySendAsync(Enc(encoder, E(42)), CancellationToken.None));
        await transport.CloseAsync(CancellationToken.None);
        Assert.Equal(TransportState.Closed, transport.State);

        await server.StopAsync(CancellationToken.None);

        Assert.Single(sink.Events);
        Assert.Equal(42, sink.Events[0].EventId.Id);
    }
}
