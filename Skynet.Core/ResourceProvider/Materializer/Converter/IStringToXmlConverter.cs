namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt IStringResource (Text) in ein IXmlResource (XDocument).
/// </summary>
public interface IStringToXmlConverter : IResourceConverter<IStringResource, IXmlResource> { }