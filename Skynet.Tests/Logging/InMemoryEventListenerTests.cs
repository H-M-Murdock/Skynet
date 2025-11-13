using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging
{
    public sealed class InMemoryEventListenerTests
    {
        /// <summary>
        /// Private Hilfsklasse nur für diese Tests. Sie simuliert einen vorab
        /// gefüllten, lesbaren Kanal, um die Listener-Tests einfach zu halten.
        /// </summary>
        private sealed class PrefilledEventChannel : IEventChannel
        {
            private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _frames;

            public PrefilledEventChannel(IEnumerable<ReadOnlyMemory<byte>> frames)
            {
                _frames = new ConcurrentQueue<ReadOnlyMemory<byte>>(frames);
            }

            public Task<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken ct)
            {
                // Explizite Implementierung, um Compiler-Fehler zu vermeiden.
                if (_frames.TryDequeue(out var frame))
                {
                    // Konvertiert den ReadOnlyMemory<byte> in einen Task<ReadOnlyMemory<byte>?>
                    return Task.FromResult<ReadOnlyMemory<byte>?>(frame);
                }

                // Gibt einen Task zurück, der ein null-Ergebnis enthält.
                return Task.FromResult<ReadOnlyMemory<byte>?>(null);
            }

            public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

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
            var channel = new PrefilledEventChannel(frames);

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

            var ch1 = new PrefilledEventChannel(new[]
            {
                new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("c1f1")),
            });
            var ch2 = new PrefilledEventChannel(new[]
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

            var ch1 = new PrefilledEventChannel(ch1Frames);
            var ch2 = new PrefilledEventChannel(ch2Frames);

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
}