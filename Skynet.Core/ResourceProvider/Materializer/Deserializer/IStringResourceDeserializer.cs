namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Fallback-Deserializer, der beliebige Textdaten in eine IStringResource wandelt.
/// </summary>
public interface IStringResourceDeserializer : IResourceDeserializer<IStringResource> { }