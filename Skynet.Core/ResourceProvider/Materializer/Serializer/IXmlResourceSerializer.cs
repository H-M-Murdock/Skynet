namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Serializer für IXmlResource (schreibt XML, UTF-8, typischerweise "application/xml").
/// </summary>
public interface IXmlResourceSerializer : IResourceSerializer<IXmlResource> { }