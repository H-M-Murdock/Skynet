// File: Skynet.Tests/Time/ClockTests.cs
using System;
using System.Threading;
using Skynet.Core;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    public class ClockTests
    {
        [Fact]
        public void FrozenClock_ReturnsConfiguredUtcNow()
        {
            var fixedUtc = new DateTime(2025, 01, 15, 12, 34, 56, DateTimeKind.Utc);
            IClock clock = new FrozenClock(fixedUtc);

            Assert.Equal(fixedUtc, clock.UtcNow);
            Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
            Assert.Equal(DateOnly.FromDateTime(fixedUtc), clock.Today);
        }

        [Fact]
        public void AdjustableClock_AdvancesByTimeSpan()
        {
            var startUtc = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);
            var clock = new AdjustableClock(startUtc);

            clock.Advance(TimeSpan.FromHours(3));
            Assert.Equal(startUtc.AddHours(3), clock.UtcNow);

            clock.Advance(TimeSpan.FromMinutes(30));
            Assert.Equal(startUtc.AddHours(3).AddMinutes(30), clock.UtcNow);
        }

        [Fact]
        public void AdjustableClock_IsThreadSafe()
        {
            var startUtc = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);
            var clock = new AdjustableClock(startUtc);

            var threads = new Thread[8];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        clock.Advance(TimeSpan.FromMilliseconds(1));
                    }
                });
            }

            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            // 8 * 1000 ms = 8000 ms
            Assert.Equal(startUtc.AddMilliseconds(8000), clock.UtcNow);
        }

        [Fact]
        public void SystemClock_ReportsReasonableNow()
        {
            IClock clock = new SystemClock();
            var before = DateTime.UtcNow.AddSeconds(-1);
            var now = clock.UtcNow;
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.True(now >= before && now <= after);
            Assert.Equal(DateOnly.FromDateTime(now), clock.Today);
        }
    }
}
