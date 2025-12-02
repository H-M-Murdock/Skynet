namespace Skynet.Core.Logging;

/// <summary>
/// Verantwortlich für das Aufräumen alter Dateien in einem Verzeichnis.
/// </summary>
public interface IFileRetentionPolicy
{
    /// <summary>
    /// Wendet die Retentions-Regeln auf das angegebene Verzeichnis an.
    /// </summary>
    /// <param name="directoryPath">Das zu prüfende Verzeichnis.</param>
    /// <param name="searchPattern">Muster der zu prüfenden Dateien (z.B. "bootstrap*.log").</param>
    void Apply(string directoryPath, string searchPattern);
}
