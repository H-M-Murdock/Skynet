// File: Skynet.Core/Time/ClockServiceCollectionExtensions.cs

using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Time
{
    public static class ClockServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the production clock (SystemClock) as IClock.
        /// </summary>
        public static IServiceCollection AddClock(this IServiceCollection services)
            => services.AddSingleton<IClock, SystemClock>();

        /// <summary>
        /// Registers a FrozenClock for tests (do not use in production).
        /// </summary>
        public static IServiceCollection AddClockFrozen(this IServiceCollection services, DateTime frozenUtc)
            => services.AddSingleton<IClock>(_ => new FrozenClock(frozenUtc));

        /// <summary>
        /// Registers an AdjustableClock for tests (do not use in production).
        /// </summary>
        public static IServiceCollection AddClockAdjustable(this IServiceCollection services, DateTime startUtc)
            => services.AddSingleton<IClock>(_ => new AdjustableClock(startUtc));
    }
}