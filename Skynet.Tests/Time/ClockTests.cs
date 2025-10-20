using System;
using System.Threading;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Tests für IClock-Implementierungen (SystemClock, FrozenClock, AdjustableClock).
    /// </summary>
    public class ClockTests
    {
        [Fact]
        public void SystemClock_Should_Return_UtcNow_Close_To_System()
        {
            // Arrange
            IClock clock = new SystemClock();

            // Act
            var before = DateTime.UtcNow;
            var actual = clock.UtcNow;
            var after = DateTime.UtcNow;

            // Assert
            // clock.UtcNow sollte zwischen before und after liegen
            Assert.True(actual >= before && actual <= after, $"UtcNow außerhalb erwarteter Bounds: {before:o} <= {actual:o} <= {after:o}");
            Assert.Equal(DateTimeKind.Utc, actual.Kind);
        }

        [Fact]
        public void FrozenClock_Should_Return_Fixed_Utc()
        {
            // Arrange
            var frozen = new DateTime(2024, 12, 31, 23, 59, 58, DateTimeKind.Utc);
            IClock clock = new FrozenClock(frozen);

            // Act
            var t1 = clock.UtcNow;
            Thread.Sleep(10);
            var t2 = clock.UtcNow;

            // Assert
            Assert.Equal(frozen, t1);
            Assert.Equal(frozen, t2);
            Assert.Equal(DateTimeKind.Utc, t1.Kind);
        }

        [Fact]
        public void AdjustableClock_Should_Advance_Time()
        {
            // Arrange
            var start = new DateTime(2025, 01, 01, 00, 00, 00, DateTimeKind.Utc);
            var delta = TimeSpan.FromMinutes(5);
            IClock clock = new AdjustableClock(start);

            // Act
            var t0 = clock.UtcNow;
            (clock as AdjustableClock)!.Advance(delta);
            var t1 = clock.UtcNow;

            // Assert
            Assert.Equal(start, t0);
            Assert.Equal(start + delta, t1);
            Assert.Equal(DateTimeKind.Utc, t1.Kind);
        }

        [Fact]
        public void IClock_Today_Should_Reflect_UtcNow_Date()
        {
            // Arrange
            var utc = new DateTime(2025, 03, 10, 12, 34, 56, DateTimeKind.Utc);
            IClock clock = new FrozenClock(utc);

            // Act
            var today = clock.Today;

            // Assert
            Assert.Equal(new DateOnly(2025, 03, 10), today);
        }

        [Fact]
        public void AdjustableClock_Should_Force_Utc_Kind_On_Local_Input()
        {
            // Arrange: lokale Zeit ohne UTC-Kind
            var local = new DateTime(2025, 01, 01, 00, 00, 00, DateTimeKind.Local);
            IClock clock = new AdjustableClock(local);

            // Act
            var t = clock.UtcNow;

            // Assert: sollte als UTC markiert sein (Wert entspricht Ticks des angegebenen Datums)
            Assert.Equal(DateTimeKind.Utc, t.Kind);
            Assert.Equal(local.Ticks, t.Ticks);
        }
    }
}
