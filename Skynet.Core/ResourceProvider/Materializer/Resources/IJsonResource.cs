using System.Text.Json.Nodes;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Eine bereits geparste JSON-Ressource (DOM).
/// </summary>
public interface IJsonResource : IStringResource
{
    /// <summary>
    /// Der Wurzel-Knoten des JSON-Dokuments (Object, Array, Value oder null).
    /// Kann null sein, wenn das JSON-Dokument literal "null" war.
    /// </summary>
    JsonNode? Node { get; }
}