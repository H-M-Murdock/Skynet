using System.Threading.Tasks;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Repräsentiert einen einzelnen Schritt im Initialisierungsprozess.
/// </summary>
public interface IBootStep
{
    /// <summary>
    /// Der lesbare Name der Komponente (z.B. "Initialize Logging").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Führt die Logik aus.
    /// </summary>
    /// <param name="context">Der Bootstrap Context.</param>
    /// <returns>Einen Status-Bericht (String), der nach Erfolg geloggt wird (z.B. "LogPath: /tmp/logs").</returns>
    Task<string> ExecuteAsync(BootstrapContext context);
}