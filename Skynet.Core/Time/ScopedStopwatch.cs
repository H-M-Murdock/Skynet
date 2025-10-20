using System;

namespace Skynet.Core.Time
{
    /// <summary>
    /// Misst die Zeitspanne innerhalb eines Scopes (using) mithilfe einer IStopwatch
    /// und liefert die verstrichene Dauer beim Dispose an einen Callback.
    /// </summary>
    public sealed class ScopedStopwatch : IDisposable
    {
        private readonly IStopwatch _stopwatch;
        private readonly Action<TimeSpan> _onDispose;
        private readonly long _start;
        private bool _disposed;

        /// <summary>
        /// Erstellt eine ScopedStopwatch.
        /// </summary>
        /// <param name="stopwatch">Monotone Zeitquelle.</param>
        /// <param name="onDispose">Callback, der die verstrichene Zeit erhält.</param>
        /// <exception cref="ArgumentNullException">Wenn stopwatch oder onDispose null sind.</exception>
        public ScopedStopwatch(IStopwatch stopwatch, Action<TimeSpan> onDispose)
        {
            _stopwatch = stopwatch ?? throw new ArgumentNullException(nameof(stopwatch));
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            _start = _stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Beendet die Messung und ruft den Callback genau einmal auf.
        /// Idempotent: Mehrfachaufrufe haben keine Wirkung.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var end = _stopwatch.GetTimestamp();
            var elapsed = _stopwatch.Elapsed(_start, end);
            _onDispose(elapsed);
        }
    }
}
