using System;
using System.Threading;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Tests für ScopedStopwatch – misst Dauer in einem using-Block.
    /// </summary>
    public class ScopedStopwatchTests
    {
        [Fact]
        public void ScopedStopwatch_Dispose_Reports_Elapsed()
        {
            // Arrange
            var sw = new Skynet.Core.Time.Stopwatch();
            TimeSpan? observed = null;

            // Act
            using (new ScopedStopwatch(sw, ts => observed = ts))
            {
                Thread.Sleep(30);
            }

            // Assert
            Assert.True(observed.HasValue);
            Assert.InRange(observed!.Value.TotalMilliseconds, 10, 120);
        }

        [Fact]
        public void ScopedStopwatch_Multiple_Dispose_Reports_Once()
        {
            // Arrange
            var sw = new Skynet.Core.Time.Stopwatch();
            int calls = 0;

            // Act
            var scope = new ScopedStopwatch(sw, _ => calls++);
            scope.Dispose();
            scope.Dispose(); // idempotent

            // Assert
            Assert.Equal(1, calls);
        }

        [Fact]
        public void ScopedStopwatch_Throws_On_Null_Arguments()
        {
            // Arrange
            var sw = new Skynet.Core.Time.Stopwatch();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => new ScopedStopwatch(null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new ScopedStopwatch(sw, null!));
        }
    }
}
