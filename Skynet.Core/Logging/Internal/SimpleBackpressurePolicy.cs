using System;

namespace Skynet.Core.Logging;

/// <summary>
/// Einfache Heuristik für Backpressure im Client:
/// - Unter lowerThreshold: keine Gegenmaßnahme (None)
/// - Zwischen lowerThreshold und upperThreshold: verwerfe neueste Anfrage (DropNewest), um Aufrufer nicht zu blockieren
/// - Ab upperThreshold: beginne, älteste Einträge zu verwerfen (DropOldest), um Frische zu bewahren
/// Hinweise:
/// - Block wird bewusst vermieden, um Produzenten-Threads nicht zu stallen.
/// - Thresholds sind relative Anteile der Kapazität und werden in konkrete Grenzen umgerechnet.
/// </summary>
public sealed class SimpleBackpressurePolicy : IBackpressurePolicy
{
    private readonly double _lowerThreshold;
    private readonly double _upperThreshold;

    /// <param name="lowerThreshold">Anteil (0..1), ab dem Gegenmaßnahmen beginnen (empf.: 0.7)</param>
    /// <param name="upperThreshold">Anteil (0..1), ab dem aggressiver reagiert wird (empf.: 0.9)</param>
    public SimpleBackpressurePolicy(double lowerThreshold = 0.7, double upperThreshold = 0.9)
    {
        if (lowerThreshold < 0 || lowerThreshold > 1) throw new ArgumentOutOfRangeException(nameof(lowerThreshold));
        if (upperThreshold < 0 || upperThreshold > 1) throw new ArgumentOutOfRangeException(nameof(upperThreshold));
        if (lowerThreshold >= upperThreshold) throw new ArgumentException("lowerThreshold muss kleiner als upperThreshold sein.");

        _lowerThreshold = lowerThreshold;
        _upperThreshold = upperThreshold;
    }

    /// <summary>
    /// Ermittelt eine Drop-Strategie basierend auf Auslastung.
    /// </summary>
    public DropMode Decide(int queueLength, int capacity)
    {
        if (capacity <= 0) return DropMode.DropNewest; // defensive: kaputte Kapazität -> verwerfe neueste Eingänge
        if (queueLength <= 0) return DropMode.None;

        var load = (double)queueLength / capacity;

        if (load < _lowerThreshold) return DropMode.None;
        if (load < _upperThreshold) return DropMode.DropNewest;
        return DropMode.DropOldest;
    }
}
