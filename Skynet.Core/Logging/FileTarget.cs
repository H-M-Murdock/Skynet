namespace Skynet.Core.Logging;

// Entscheidet Ziel-Datei und Rotation (zeit-/größenbasiert).
public sealed record FileTarget(string FullPath);