// C#
// Skynet.Core.ResourceProvider/ICsvResource.cs
namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Eine geparste CSV-Ressource (In-Memory).
/// </summary>
public interface ICsvResource : IStringResource
{
    /// <summary>Header-Zeile (falls vorhanden), sonst leer.</summary>
    IReadOnlyList<string> Headers { get; }

    /// <summary>Datenzeilen. Jede Zeile ist eine Liste von Spaltenwerten.</summary>
    IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    char? Delimiter { get; }
    char? Quote { get; }
}
