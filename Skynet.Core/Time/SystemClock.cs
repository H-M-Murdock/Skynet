// File: Skynet.Core/Time/SystemClock.cs

namespace Skynet.Core.Time
{
    /// <summary>
    /// Production clock sourcing from DateTime.UtcNow.
    /// Verwenden, wenn reale aktuelle Zeit benötigt wird.
    /// Hinweis: Für deterministische Tests stattdessen FrozenClock/AdjustableClock nutzen.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        /// <summary>
        /// Liefert die aktuelle UTC-Zeit. Delegiert an DateTime.UtcNow.
        /// </summary>
        public DateTime UtcNow => DateTime.UtcNow;
    }
}