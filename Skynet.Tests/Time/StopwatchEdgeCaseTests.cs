using System;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Edge-Case-Tests für die monotone Stopwatch-Implementierung.
    /// </summary>
    public class StopwatchEdgeCaseTests
    {
        [Fact]
        public void Elapsed_Allows_Negative_Delta_When_End_Before_Start()
        {
            // Arrange
            IStopwatch sw = new Skynet.Core.Time.Stopwatch();
            var t2 = sw.GetTimestamp();
            var t1 = t2 + 10; // start > end

            // Act
            var elapsed = sw.Elapsed(t1, t2);

            // Assert
            Assert.True(elapsed < TimeSpan.Zero);
        }

        [Fact]
        public void Elapsed_Should_Not_Overflow_On_Very_Large_Delta()
        {
            // Arrange
            IStopwatch sw = new Skynet.Core.Time.Stopwatch();

            // Wir simulieren einen sehr großen Delta-Wert, der in TimeSpan.Ticks passen könnte,
            // aber die double-Multiplikation clampen sollte.
            // Hinweis: Wir konstruieren Werte künstlich (keine echte Zeit).
            long hugeDelta = long.MaxValue / 2;
            long start = 0;
            long end = start + hugeDelta;

            // Act
            var elapsed = sw.Elapsed(start, end);

            // Assert
            Assert.True(elapsed > TimeSpan.Zero);
            // Sollte nicht zu Overflow führen (TimeSpan.MaxValue ~ 9e18 Ticks)
            Assert.InRange(elapsed, TimeSpan.Zero, TimeSpan.MaxValue);
        }

        [Fact]
        public void Elapsed_Zero_When_Start_Equals_End()
        {
            // Arrange
            IStopwatch sw = new Skynet.Core.Time.Stopwatch();
            var ts = sw.GetTimestamp();

            // Act
            var elapsed = sw.Elapsed(ts, ts);

            // Assert
            Assert.Equal(TimeSpan.Zero, elapsed);
        }
    }
}
