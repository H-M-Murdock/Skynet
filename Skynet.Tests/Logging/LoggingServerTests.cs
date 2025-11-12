using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Skynet.Core.Logging;

namespace Skynet.Tests.Logging
{
    public sealed class LoggingServerTests : IAsyncLifetime
    {
        private readonly InMemoryEventListener _listener;
        private readonly MemoryLogSink _sink;
        private readonly SingleSinkRouter _router;
        private readonly FakeEncoder _encoder;
        private readonly LoggingServer _server;

        public LoggingServerTests()
        {
            _listener = new InMemoryEventListener();
            _sink     = new MemoryLogSink(capacity: 1_000);
            _router   = new SingleSinkRouter(_sink);
            _encoder  = new FakeEncoder();

            var opts = new LoggingServerOptions
            {
                WorkerCount = 2,
                MaxQueueLength = 1024,
                QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                PeriodicFlushInterval = TimeSpan.FromMilliseconds(200)
            };

            _server = new LoggingServer(
                _listener,
                _encoder,
                _router,
                new DefaultLogEventMaterializer(), // <— neu: Materializer
                opts);
        }

        private sealed class TestEncoder : ILogEventEncoder
        {
            public string ContentType => "application/x-test";
            public string Version => "1.0";

            public ReadOnlyMemory<byte> Encode(ILogEvent evt)
            {
                var id = evt.EventId.Id;
                return Encoding.UTF8.GetBytes($"id:{id}");
            }

            public bool TryDecode(ReadOnlySpan<byte> payload, out ILogEvent? evt)
            {
                var s = Encoding.UTF8.GetString(payload);
                if (!s.StartsWith("id:", StringComparison.Ordinal) || !int.TryParse(s.AsSpan(3), out var n))
                {
                    evt = null;
                    return false;
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
        private static MutableLogEvent E(int id) => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Information,
            EventId = new EventId(id, "E"),
            GlobalEventId = id.ToString("n"),
            State = new List<KeyValuePair<string, object?>>()
        };
        
        private static ReadOnlyMemory<byte> Enc(ILogEventEncoder enc, ILogEvent evt) => enc.Encode(evt);
        
        public async Task InitializeAsync() => await _server.StartAsync(CancellationToken.None);
        public async Task DisposeAsync()     => await _server.DisposeAsync();

        [Fact(DisplayName = "EndToEnd: Alle MutableLogEvents werden dispatcht")]
        public async Task EndToEnd_MultipleChannels_AllEventsArrive()
        {
            var ch1 = new InMemoryEventChannel(new[]
            {
                ToBytes("c1-e1"), ToBytes("c1-e2"), ToBytes("c1-e3")
            });
            var ch2 = new InMemoryEventChannel(new[]
            {
                ToBytes("c2-e1"), ToBytes("c2-e2"), ToBytes("c2-e3")
            });

            _listener.EnqueueChannel(ch1);
            _listener.EnqueueChannel(ch2);

            await Task.Delay(300);
            await _server.StopAsync(CancellationToken.None);

            var snap = _sink.Snapshot().OfType<MutableLogEvent>().ToList();
            Assert.True(snap.Count >= 6);
            Assert.Contains(snap, e => e.Operation == "c1-e1");
            Assert.Contains(snap, e => e.Operation == "c2-e3");
        }

        [Fact]
        public async Task Overflow_DropsOldest_KeepsLatest()
        {
            // Arrange
            var listener = new InMemoryEventListener();
            var encoder = new TestEncoder();
            var sink = new MemoryLogSink(capacity: 10);
            var router = new SingleSinkRouter(sink);
            var materializer = new DefaultLogEventMaterializer();

            var server = new LoggingServer(
                listener, encoder, router, materializer,
                new LoggingServerOptions
                {
                    MaxQueueLength = 5,
                    QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                    WorkerCount = 1,
                    PeriodicFlushInterval = TimeSpan.Zero
                });
            await server.StartAsync(CancellationToken.None);

            var transport = new InMemoryEventTransport(listener, capacity: 15);
            await transport.ConnectAsync(CancellationToken.None);

            // Act
            for (var i = 0; i < 10; i++)
            {
                Assert.True(await transport.TrySendAsync(Enc(encoder, E(i)), CancellationToken.None));
            }

            // 1. Der Client (Test) signalisiert: "Ich bin fertig mit Senden für DIESEN Transport".
            // Dies bewirkt, dass der zugehörige ReaderLoop im Server endet.
            await transport.CloseAsync(CancellationToken.None);

            // 2. Der Test signalisiert: "Fahre jetzt den GESAMTEN Server herunter".
            // Die jetzt robuste StopAsync-Methode orchestriert den Rest.
            await server.StopAsync(CancellationToken.None);

            // Assert
            Assert.Equal(5, sink.Count);
            var ids = sink.Snapshot().Select(e => e.EventId.Id).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 5, 6, 7, 8, 9 }, ids);
        }

        [Fact(DisplayName = "Stop ist idempotent")]
        public async Task Stop_IsIdempotent()
        {
            var sink = new MemoryLogSink(capacity: 5);
            var router = new SingleSinkRouter(sink);
            var server = new LoggingServer(
                _listener, _encoder, router,
                new DefaultLogEventMaterializer(), // <— neu
                new LoggingServerOptions { PeriodicFlushInterval = TimeSpan.Zero });

            await server.StartAsync(CancellationToken.None);
            await server.StopAsync(CancellationToken.None);
            await server.StopAsync(CancellationToken.None); // zweiter Aufruf ohne Exception

            Assert.True(true);
        }

        private static ReadOnlyMemory<byte> ToBytes(string s) => new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(s));

        // FakeEncoder erzeugt MutableLogEvent aus UTF8-Text; legt Text in Operation ab
        private sealed class FakeEncoder : ILogEventEncoder
        {
            public string ContentType => "application/x-test";
            public string Version => "1.0";

            public ReadOnlyMemory<byte> Encode(ILogEvent evt) => throw new NotSupportedException();

            public bool TryDecode(ReadOnlySpan<byte> payload, out ILogEvent? evt)
            {
                var msg = Encoding.UTF8.GetString(payload);
                evt = new MutableLogEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Operation = msg,
                    CategoryName = "Test"
                };
                return true;
            }
        }
    }
}
