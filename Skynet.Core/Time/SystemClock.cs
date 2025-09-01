// File: Skynet.Core/Time/SystemClock.cs

namespace Skynet.Core.Time
{
    /// <summary>
    /// Production clock sourcing from DateTime.UtcNow.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}