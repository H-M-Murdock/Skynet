namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Spezialisierter Deserializer für XML-Daten. Erzeugt ein IXmlResource DOM (XDocument).
/// Erwartet "application/xml" oder "text/xml".
/// </summary>
public interface IXmlResourceDeserializer : IResourceDeserializer<IXmlResource> { }