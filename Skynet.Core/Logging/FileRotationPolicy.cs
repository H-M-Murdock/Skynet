namespace Skynet.Core.Logging;

// Entscheidet Ziel-Datei und Rotation (zeit-/größenbasiert).
public sealed record FileTarget(string FullPath);

public interface IFileRotationPolicy
{
    // Liefert die aktuelle Zieldatei (abhängig von Template/Datum/Tenant/Component).
    FileTarget ResolveTarget(ILogEvent evt, DateTimeOffset now);

    // True, wenn für die Datei ein Roll-Over nötig ist (z. B. Größe überschritten oder Tagwechsel).
    bool ShouldRotate(FileTarget target, long currentBytes, DateTimeOffset now);
}
