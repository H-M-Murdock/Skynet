// C#
// Skynet.Core.ResourceProvider/ICsvResource.cs
namespace Skynet.Core.ResourceProvider;

// Neutrale CSV-Repräsentation (UTF-8, in-memory, string-basiert)
public interface ICsvResource : IStringResource
{
    // Optional: Header. Kann leer sein, wenn keine Kopfzeile existiert.
    IReadOnlyList<string> Headers { get; }

    // Zeilen als Liste von Feldern (bereits geparst/entquotet).
    IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    // Optional: Trenn- und Quote-Zeichen, falls relevant (Dokumentation der Implementierung).
    char? Delimiter { get; }
    char? Quote { get; }
}
