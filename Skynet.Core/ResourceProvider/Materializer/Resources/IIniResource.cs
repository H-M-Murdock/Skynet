// C#
// Skynet.Core.ResourceProvider/IIniResource.cs
namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Repräsentiert INI-Dateien oder flache Key-Value-Listen (.env).
/// </summary>
public interface IIniResource : IStringResource
{
    /// <summary>
    /// Hierarchischer Zugriff: [Section] -> Key -> Value.
    /// Keys sind case-insensitive (je nach Implementierung).
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Sections { get; }

    /// <summary>
    /// Flache Sicht aller Einträge (z.B. für Environment-Variablen-Emulation).
    /// Bei INI werden Keys oft als "Section:Key" normalisiert.
    /// </summary>
    IReadOnlyDictionary<string, string?> FlatEntries { get; }
}
