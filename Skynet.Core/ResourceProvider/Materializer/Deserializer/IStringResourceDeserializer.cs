// C#
// Skynet.Core.Materialization/Deserializers/IStringResourceDeserializer.cs

namespace Skynet.Core.ResourceProvider;

// Deserializer für IStringResource (UTF-8, z. B. "text/plain; charset=utf-8")
public interface IStringResourceDeserializer : IResourceDeserializer<IStringResource> { }
