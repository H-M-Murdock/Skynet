// C#
// Skynet.Core.Materialization/Converters/IBinaryToStringConverter.cs

namespace Skynet.Core.ResourceProvider;

// IInMemoryBinaryResource -> IStringResource (UTF-8)
public interface IBinaryToStringConverter : IResourceConverter<IInMemoryBinaryResource, IStringResource> { }
