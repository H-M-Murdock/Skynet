// File: Skynet.Core/Time/FrozenClock.cs

namespace Skynet.Core.Time
{
    /// <summary>
    /// Fixierte Zeitquelle für deterministische Tests.
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

        /// <summary>
        /// Gibt stets die beim Erzeugen gesetzte UTC-Zeit zurück.
        /// </summary>
        public DateTime UtcNow => _utc;
    }
}