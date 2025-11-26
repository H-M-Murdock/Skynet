namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt IStringResource (Text) in ein IJsonResource (DOM).
/// </summary>
public interface IStringToJsonConverter : IResourceConverter<IStringResource, IJsonResource> { }