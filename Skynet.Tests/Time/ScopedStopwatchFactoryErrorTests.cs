using System;
using System.Threading;
using Skynet.Core.Logging;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Tests für Fehlerverhalten der ScopedStopwatchFactory (LogAsync wirft).
    /// </summary>
    public class ScopedStopwatchFactoryErrorTests
    {
        private sealed class ThrowingLoggingClient : ILoggingClient
        {
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask LogAsync(ILogEvent evt, CancellationToken ct) => throw new InvalidOperationException("boom");
            public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(bool drain, CancellationToken ct) => Task.CompletedTask;
            public long DroppedCount => 0;
            public int QueueLength => 0;
            public int QueueCapacity => 0;
        }

        [Fact]
        public void Dispose_Should_Not_Throw_When_LoggingClient_Fails()
        {
            // Arrange
            var sw = new Skynet.Core.Time.Stopwatch();
            var client = new ThrowingLoggingClient();
            var factory = new ScopedStopwatchFactory(sw, client);

            // Act + Assert
            // Da die Factory fire-and-forget auf LogAsync zugreift, darf Dispose nicht werfen.
            using (factory.Start("Test.Operation"))
            {
                // nop
            }
        }
    }
}
