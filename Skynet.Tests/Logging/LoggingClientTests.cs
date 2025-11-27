using System.Text;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class LoggingClientTests
{
    // ——— Deterministischer Test-Encoder (keine gepoolten Instanzen) ———
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
            { evt = null; return false; }

            evt = new MutableLogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = LogLevel.Information,
                EventId = new EventId(n, "E")
            };
            return true;
        }
    }

    // ——— Test-Hilfen ———
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
        EventId = new EventId(id, "E")
    };

    [Fact]
    public async Task EndToEnd_Client_Sends_To_Server_Sink()
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
                PeriodicFlushInterval = TimeSpan.Zero,
                QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                AcceptBackoff = TimeSpan.FromMilliseconds(5)
            });
        await server.StartAsync(CancellationToken.None);

        var transportFactory   = new InMemoryEventTransportFactory(listener, defaultCapacity: 16);
        var backpressurePolicy = new SimpleBackpressurePolicy(); // nutzt DropNewest/DropOldest heuristisch

        // FIX: Neue optionale Parameter mit null/default übergeben
        var client = new LoggingClient(
            transportFactory,
            encoder,
            backpressurePolicy,
            enrichers: null,
            redactionPolicy: null,
            sizeLimiter: null,
            new LoggingClientOptions
            {
                QueueCapacity = 64,
                BatchSize = 8,
                FlushInterval = TimeSpan.FromMilliseconds(50)
            });

        await client.StartAsync(CancellationToken.None);

        // Act
        await client.LogAsync(E(1), default);
        await client.LogAsync(E(2), default);
        await client.FlushAsync(CancellationToken.None);

        await server.StopAsync(CancellationToken.None);
        await client.StopAsync(drain: true, CancellationToken.None);

        // Assert
        var ids = sink.Events.Select(e => e.EventId.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2 }, ids);
        Assert.Equal(0, client.QueueLength);
    }

    [Fact]
    public async Task Client_Backpressure_Drops_When_No_Server_Consumes()
    {
        // Kein Server — wir testen gezielt die Client-Backpressure am Enqueue.
        var listener = new InMemoryEventListener();
        await listener.BindAsync(CancellationToken.None);

        var encoder = new TestEncoder();
        var tf      = new InMemoryEventTransportFactory(listener, defaultCapacity: 2);

        // Policy: ab 50% volle Queue reagieren, ab 80% aggressiver.
        var policy  = new SimpleBackpressurePolicy(lowerThreshold: 0.5, upperThreshold: 0.8);

        // FIX: Neue optionale Parameter
        var client = new LoggingClient(
            tf, encoder, policy,
            enrichers: null, redactionPolicy: null, sizeLimiter: null,
            new LoggingClientOptions
            {
                QueueCapacity = 2,   // sehr klein, damit sofort greift
                BatchSize = 16,
                FlushInterval = TimeSpan.FromMilliseconds(200)
            });

        // *** WICHTIG: NICHT starten! ***
        // Wir füllen die Queue vor StartAsync, damit der Sender-Loop nichts wegliest
        for (int i = 0; i < 50; i++)
            await client.LogAsync(E(i), default);

        // Jetzt sollte die Policy mehrfach gegriffen haben (DropOldest),
        // ohne dass etwas versendet wurde:
        Assert.True(client.DroppedCount > 0);

        // Optional: jetzt erst starten und sauber stoppen (soll nicht hängen)
        await client.StartAsync(CancellationToken.None);
        await client.StopAsync(drain: false, CancellationToken.None);
    }

    [Fact]
    public async Task Flush_Empties_Client_Queue_And_Transport()
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

        var tf     = new InMemoryEventTransportFactory(listener, defaultCapacity: 4);
        var policy = new SimpleBackpressurePolicy();

        // FIX: Neue optionale Parameter
        var client = new LoggingClient(
            tf, encoder, policy,
            enrichers: null, redactionPolicy: null, sizeLimiter: null,
            new LoggingClientOptions
            {
                QueueCapacity = 8,
                BatchSize = 4,
                FlushInterval = TimeSpan.FromMilliseconds(200)
            });

        await client.StartAsync(CancellationToken.None);

        // Act: vier Events -> ein voller Batch
        for (int i = 0; i < 4; i++)
            await client.LogAsync(E(i), default);

        await client.FlushAsync(CancellationToken.None);
        await server.StopAsync(CancellationToken.None);
        await client.StopAsync(drain: true, CancellationToken.None);

        // Assert
        var ids = sink.Events.Select(x => x.EventId.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, ids);
        Assert.Equal(0, client.QueueLength);
        Assert.Equal(0, client.DroppedCount);
    }

    private sealed class AlwaysDropOldestPolicy : IBackpressurePolicy
    {
        public DropMode Decide(int _, int __) => DropMode.DropOldest;
    }

    [Fact]
    public async Task LogAsync_Counts_Drops_When_DropOldest()
    {
        var listener = new InMemoryEventListener();
        // Transport/Server sind hier egal; wir starten den Client NICHT.
        var encoder  = new TestEncoder();
        var tf       = new InMemoryEventTransportFactory(listener, defaultCapacity: 2);
        var policy   = new AlwaysDropOldestPolicy();

        // FIX: Neue optionale Parameter
        var client = new LoggingClient(tf, encoder, policy, 
            enrichers: null, redactionPolicy: null, sizeLimiter: null,
            new LoggingClientOptions { QueueCapacity = 2 });

        // zwei passen rein:
        await client.LogAsync(E(1), default);
        await client.LogAsync(E(2), default);

        // alle weiteren triggern DropOldest:
        for (int i = 3; i <= 10; i++)
            await client.LogAsync(E(i), default);

        Assert.True(client.DroppedCount > 0);
    }
}