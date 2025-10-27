using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        [Fact(DisplayName = "Overflow: MemoryLogSink droppt älteste Events")]
        public async Task Overflow_DropsOldest_KeepsLatest()
        {
            var smallSink = new MemoryLogSink(capacity: 5);
            var smallRouter = new SingleSinkRouter(smallSink);
            var opts = new LoggingServerOptions
            {
                WorkerCount = 1,
                MaxQueueLength = 64,
                QueueFullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                PeriodicFlushInterval = TimeSpan.Zero
            };
            var tmpServer = new LoggingServer(
                _listener, _encoder, smallRouter,
                new DefaultLogEventMaterializer(), // <— neu
                opts);
            await tmpServer.StartAsync(CancellationToken.None);

            var many = Enumerable.Range(0, 20)
                                 .Select(i => ToBytes($"e{i:D2}"))
                                 .ToArray();
            var ch = new InMemoryEventChannel(many);
            _listener.EnqueueChannel(ch);

            await Task.Delay(200);
            await tmpServer.StopAsync(CancellationToken.None);

            var snap = smallSink.Snapshot().OfType<MutableLogEvent>().ToList();
            Assert.Equal(5, snap.Count);
            // Die letzten 5 Operation-Werte sollten im Sink enthalten sein
            var expected = Enumerable.Range(15, 5).Select(i => $"e{i:D2}");
            Assert.True(expected.SequenceEqual(snap.Select(e => e.Operation)));
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
