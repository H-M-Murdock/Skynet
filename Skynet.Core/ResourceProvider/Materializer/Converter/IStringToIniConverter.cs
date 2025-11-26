namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt IStringResource (Text) in ein IIniResource (Sections/Keys).
/// </summary>
public interface IStringToIniConverter : IResourceConverter<IStringResource, IIniResource> { }