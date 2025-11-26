namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Spezialisierter Deserializer für INI-Dateien oder .env-Dateien.
/// Erwartet typischerweise "text/plain" oder spezifische INI-Mime-Types.
/// </summary>
public interface IIniResourceDeserializer : IResourceDeserializer<IIniResource> { }