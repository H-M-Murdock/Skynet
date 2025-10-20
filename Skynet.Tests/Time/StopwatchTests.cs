using System;
using System.Diagnostics;
using System.Threading;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Tests für die monotone Stopwatch-Implementierung.
    /// </summary>
    public class StopwatchTests
    {
        [Fact]
        public void GetTimestamp_Should_Be_Monotonic_NonDecreasing()
        {
            // Arrange
            IStopwatch sw = new Skynet.Core.Time.Stopwatch();

            // Act
            var t1 = sw.GetTimestamp();
            var t2 = sw.GetTimestamp();

            // Assert
            Assert.True(t2 >= t1, $"t2 {t2} sollte >= t1 {t1} sein");
        }

        [Fact]
        public void Elapsed_Should_Approximate_Real_Time()
        {
            // Arrange
            IStopwatch sw = new Skynet.Core.Time.Stopwatch();
            var start = sw.GetTimestamp();

            // Act
            Thread.Sleep(50);
            var end = sw.GetTimestamp();
            var elapsed = sw.Elapsed(start, end);

            // Assert
            // 50 ms ± 30 ms Toleranz (CI/Jitter)
            Assert.InRange(elapsed.TotalMilliseconds, 20, 120);
        }

        [Fact]
        public void ElapsedSince_Should_Match_Elapsed()
        {
            // Arrange
            IStopwatch sw = new Skynet.Core.Time.Stopwatch();
            var start = sw.GetTimestamp();

            // Act
            Thread.Sleep(10);
            var e1 = sw.ElapsedSince(start);
            var e2 = sw.Elapsed(start, sw.GetTimestamp());

            // Assert
            var diff = Math.Abs((e1 - e2).TotalMilliseconds);
            Assert.InRange(diff, 0, 10);
        }

        [Fact]
        public void Elapsed_With_Zero_Delta_Is_Zero()
        {
            // Arrange
            IStopwatch sw = new Skynet.Core.Time.Stopwatch();
            var ts = sw.GetTimestamp();

            // Act
            var e = sw.Elapsed(ts, ts);

            // Assert
            Assert.Equal(TimeSpan.Zero, e);
        }
    }
}
