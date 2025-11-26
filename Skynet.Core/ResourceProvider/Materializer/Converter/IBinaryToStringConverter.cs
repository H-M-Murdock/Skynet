namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt IInMemoryBinaryResource (Bytes) in IStringResource (UTF-8 Text).
/// </summary>
public interface IBinaryToStringConverter : IResourceConverter<IInMemoryBinaryResource, IStringResource> { }