namespace Skynet.Core.Logging;

public interface IBackpressurePolicy
{
    // Liefert eine Entscheidung basierend auf aktueller Queue-Länge/Kapazität.
    DropMode Decide(int queueLength, int capacity);
}