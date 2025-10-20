namespace Skynet.Core.Time;

/// <summary>
/// Monotone Zeitquelle für Dauer-/Latenzmessungen, unabhängig von der Systemuhr.
/// Basierend auf einer hochauflösenden, monoton steigenden Taktquelle (z. B. Stopwatch).
/// </summary>
public interface IStopwatch
{
    /// <summary>
    /// Liefert einen monoton steigenden Roh-Zeitstempel (Implementation-defined Einheiten).
    /// Nur für Differenzen verwenden, nicht persistieren.
    /// </summary>
    long GetTimestamp();

    /// <summary>Berechnet die verstrichene Zeit zwischen zwei Zeitstempeln.</summary>
    TimeSpan Elapsed(long startTimestamp, long endTimestamp);

    /// <summary>Bequeme Variante: verstrichene Zeit seit startTimestamp.</summary>
    TimeSpan ElapsedSince(long startTimestamp);
}