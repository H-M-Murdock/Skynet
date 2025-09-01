// File: Skynet.Core/Time/AdjustableClock.cs

namespace Skynet.Core.Time
{
    /// <summary>
    /// Manually advanceable, thread-safe clock for integration tests.
    /// </summary>
    public sealed class AdjustableClock : IClock
    {
        private long _ticksUtc;

        public AdjustableClock(DateTime startUtc)
        {
            var utc = startUtc.Kind == DateTimeKind.Utc
                ? startUtc
                : DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);

            _ticksUtc = utc.Ticks;
        }

        public DateTime UtcNow => new DateTime(Interlocked.Read(ref _ticksUtc), DateTimeKind.Utc);

        public void Advance(TimeSpan delta) => Interlocked.Add(ref _ticksUtc, delta.Ticks);
    }
}