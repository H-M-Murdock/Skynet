// C#
// Skynet.Core.ResourceProvider/IIniResource.cs
namespace Skynet.Core.ResourceProvider;

// Neutrale INI/.env-Repräsentation (UTF-8, in-memory, string-basiert)
public interface IIniResource : IStringResource
{
    // Optional: Sektionen (INI-Stil). Key: Sektionsname, Value: Key-Value-Paare innerhalb der Sektion.
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Sections { get; }

    // Flache Sicht (z. B. für .env oder sektionenlose INI). Keys sind normalisiert (Implementierung definiert Konvention).
    IReadOnlyDictionary<string, string?> FlatEntries { get; }
}
