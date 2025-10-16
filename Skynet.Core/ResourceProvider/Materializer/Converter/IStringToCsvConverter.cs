// C#
// Skynet.Core.Materialization/Converters/IStringToCsvConverter.cs

namespace Skynet.Core.ResourceProvider;

// IStringResource -> ICsvResource
public interface IStringToCsvConverter : IResourceConverter<IStringResource, ICsvResource> { }
