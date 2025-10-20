// File: Skynet.Core/Time/MonotonicClock.cs

using System.Diagnostics;

namespace Skynet.Core.Time
{
    /// <summary>
    /// Stopwatch-basierte monotone Uhr. Ideal für Latenz-/Timeout-Messungen.
    /// Nutzt Stopwatch.GetTimestamp() und konvertiert via Frequency in TimeSpan.
    /// </summary>
    public sealed class Stopwatch : IStopwatch
    {
        private static readonly double TicksPerStopwatchTick = (double)TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency;

        public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

        public TimeSpan Elapsed(long startTimestamp, long endTimestamp)
        {
            var deltaTicksStopwatch = endTimestamp - startTimestamp;

            // double-Multiplikation, danach clampen in den Bereich von long-Min/Max und TimeSpan.Min/Max
            double ticksDouble = deltaTicksStopwatch * TicksPerStopwatchTick;

            // Clamp in long-Bereich, damit der ctor von TimeSpan sicher ist
            if (ticksDouble > long.MaxValue) ticksDouble = long.MaxValue;
            if (ticksDouble < long.MinValue) ticksDouble = long.MinValue;

            var timeSpanTicks = (long)ticksDouble;

            // Zusätzlich gegen TimeSpan-Grenzen clampen (obwohl ctor mit long schon prüft)
            if (timeSpanTicks > TimeSpan.MaxValue.Ticks) timeSpanTicks = TimeSpan.MaxValue.Ticks;
            if (timeSpanTicks < TimeSpan.MinValue.Ticks) timeSpanTicks = TimeSpan.MinValue.Ticks;

            return new TimeSpan(timeSpanTicks);
        }

        /// <summary>
        /// Bequeme Variante: verstrichene Zeit seit startTimestamp bis jetzt.
        /// </summary>
        public TimeSpan ElapsedSince(long startTimestamp)
            => Elapsed(startTimestamp, GetTimestamp());
    }
}
