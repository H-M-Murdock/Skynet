// File: Skynet.Core/Time/FrozenClock.cs

namespace Skynet.Core.Time
{
    /// <summary>
    /// Fixed time for deterministic tests.
    /// </summary>
    public sealed class FrozenClock : IClock
    {
        private readonly DateTime _utc;

        public FrozenClock(DateTime utcNow)
        {
            _utc = utcNow.Kind == DateTimeKind.Utc
                ? utcNow
                : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        public DateTime UtcNow => _utc;
    }
}