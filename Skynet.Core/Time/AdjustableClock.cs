// File: Skynet.Core/Time/AdjustableClock.cs

namespace Skynet.Core.Time
{
    /// <summary>
    /// Manuell voranschreitbare, thread-sichere Uhr für Integrations-/Akzeptanztests.
    /// Achtung: Nicht in Produktion verwenden.
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

        /// <summary>
        /// Aktuelle UTC-Zeit des verstellbaren Clocks. Thread-sicher via Interlocked.Read.
        /// </summary>
        public DateTime UtcNow => new DateTime(Interlocked.Read(ref _ticksUtc), DateTimeKind.Utc);

        /// <summary>
        /// Stellt die Uhr um delta vor (kann negativ sein, um zurückzustellen).
        /// Thread-sicher via Interlocked.Add.
        /// </summary>
        public void Advance(TimeSpan delta) => Interlocked.Add(ref _ticksUtc, delta.Ticks);
    }
}