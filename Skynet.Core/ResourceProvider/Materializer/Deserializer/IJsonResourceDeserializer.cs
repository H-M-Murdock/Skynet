namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Spezialisierter Deserializer für JSON-Daten. Erzeugt ein IJsonResource DOM.
/// Erwartet "application/json".
/// </summary>
public interface IJsonResourceDeserializer : IResourceDeserializer<IJsonResource> { }