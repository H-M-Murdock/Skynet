// Client-Orchestrierung: Pufferung, Backpressure-Entscheidungen, Lifecycle, Nutzung von Enrichers/Policies/Encoder.

namespace Skynet.Core.Logging;

public enum DropMode
{
    // Entfernt die ältesten Einträge im Puffer (bevorzugt für "frische" Sicht).
    DropOldest,
    // Verwirft den gerade eingehenden Eintrag (keine Puffer-Manipulation).
    DropNewest,
    // Blockiert (bis Kapazität frei) – ACHTUNG: Kann Aufrufer-Threads verzögern, eher vermeiden.
    Block,
    // Keine Backpressure – nur sinnvoll, wenn Transport selbst begrenzt/persistiert. Vorsicht vor OOM.
    None
}