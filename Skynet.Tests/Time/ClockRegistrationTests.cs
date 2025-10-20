using System;
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Tests für die DI-Registrierung der Uhren über ClockServiceCollectionExtensions.
    /// </summary>
    public class ClockRegistrationTests
    {
        [Fact]
        public void AddClock_Should_Register_SystemClock_As_Singleton_IClock()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddClock();
            var provider = services.BuildServiceProvider();

            // Assert
            var clock1 = provider.GetRequiredService<IClock>();
            var clock2 = provider.GetRequiredService<IClock>();

            Assert.IsType<SystemClock>(clock1);
            Assert.Same(clock1, clock2);
        }

        [Fact]
        public void AddClockFrozen_Should_Register_FrozenClock_With_Provided_Time()
        {
            // Arrange
            var services = new ServiceCollection();
            var frozen = new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc);

            // Act
            services.AddClockFrozen(frozen);
            var provider = services.BuildServiceProvider();

            // Assert
            var clock = provider.GetRequiredService<IClock>();
            Assert.IsType<FrozenClock>(clock);
            Assert.Equal(frozen, clock.UtcNow);
        }

        [Fact]
        public void AddClockAdjustable_Should_Register_AdjustableClock_With_Provided_Start()
        {
            // Arrange
            var services = new ServiceCollection();
            var start = new DateTime(2025, 06, 07, 08, 09, 10, DateTimeKind.Utc);

            // Act
            services.AddClockAdjustable(start);
            var provider = services.BuildServiceProvider();

            // Assert
            var clock = provider.GetRequiredService<IClock>();
            Assert.IsType<AdjustableClock>(clock);
            Assert.Equal(start, clock.UtcNow);
        }

        [Fact]
        public void Multiple_Registrations_Last_Wins()
        {
            // Arrange
            var services = new ServiceCollection();
            var frozen = new DateTime(2030, 01, 01, 00, 00, 00, DateTimeKind.Utc);

            // Act
            services.AddClock();                // IClock => SystemClock
            services.AddClockFrozen(frozen);    // IClock => FrozenClock (überschreibt)
            var provider = services.BuildServiceProvider();

            // Assert
            var clock = provider.GetRequiredService<IClock>();
            Assert.IsType<FrozenClock>(clock);
            Assert.Equal(frozen, clock.UtcNow);
        }
    }
}
