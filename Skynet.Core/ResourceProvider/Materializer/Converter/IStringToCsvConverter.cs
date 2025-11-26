namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt IStringResource (Text) in ein ICsvResource (Zeilen/Spalten).
/// </summary>
public interface IStringToCsvConverter : IResourceConverter<IStringResource, ICsvResource> { }