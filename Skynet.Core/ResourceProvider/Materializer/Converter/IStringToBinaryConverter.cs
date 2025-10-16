// C#
// Skynet.Core.Materialization/Converters/IStringToBinaryConverter.cs

namespace Skynet.Core.ResourceProvider;

// IStringResource (UTF-8) -> IInMemoryBinaryResource
public interface IStringToBinaryConverter : IResourceConverter<IStringResource, IInMemoryBinaryResource> { }
