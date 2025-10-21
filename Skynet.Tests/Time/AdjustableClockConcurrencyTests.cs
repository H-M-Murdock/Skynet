using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Thread-Safety-Tests für AdjustableClock unter Parallelität.
    /// </summary>
    public class AdjustableClockConcurrencyTests
    {
        [Fact]
        public async Task AdjustableClock_Should_Handle_Concurrent_Advance_And_Reads_Consistently()
        {
            // Arrange
            var start = new DateTime(2025, 01, 01, 00, 00, 00, DateTimeKind.Utc);
            var clock = new AdjustableClock(start);

            int writers = 8;
            int incrementsPerWriter = 10_000;
            var delta = TimeSpan.FromTicks(1); // minimaler Tick
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act
            var writerTasks = Enumerable.Range(0, writers).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < incrementsPerWriter; i++)
                {
                    clock.Advance(delta);
                }
            }, cts.Token)).ToArray();

            // Parallel: Leser, die währenddessen konsistente UtcNow lesen
            long observedKindViolations = 0;
            var readerTask = Task.Run(() =>
            {
                while (!Task.WhenAll(writerTasks).IsCompleted)
                {
                    var t = clock.UtcNow;
                    if (t.Kind != DateTimeKind.Utc) Interlocked.Increment(ref observedKindViolations);
                }
            }, cts.Token);

            await Task.WhenAll(writerTasks);
            await readerTask;

            // Assert
            Assert.Equal(0, observedKindViolations);
            var expected = start + TimeSpan.FromTicks((long)writers * incrementsPerWriter * delta.Ticks);
            Assert.Equal(expected, clock.UtcNow);
        }
    }
}
