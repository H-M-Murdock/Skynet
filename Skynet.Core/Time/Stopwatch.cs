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
            var deltaTicks = endTimestamp - startTimestamp;
            // Umrechnung in TimeSpan-Ticks (long) mit saturierender Klammerung
            var timespanTicks = (long)(deltaTicks * TicksPerStopwatchTick);
            return new TimeSpan(timespanTicks);
        }

        public TimeSpan ElapsedSince(long startTimestamp)
            => Elapsed(startTimestamp, GetTimestamp());
    }
}
