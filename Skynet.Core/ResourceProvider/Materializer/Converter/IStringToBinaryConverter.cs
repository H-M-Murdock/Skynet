namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt IStringResource zurück in IInMemoryBinaryResource (UTF-8 Bytes).
/// </summary>
public interface IStringToBinaryConverter : IResourceConverter<IStringResource, IInMemoryBinaryResource> { }