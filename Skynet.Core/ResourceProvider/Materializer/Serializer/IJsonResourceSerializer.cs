// C#
// Skynet.Core.Materialization/Serializers/IJsonResourceSerializer.cs
using Skynet.Core.Tenant;
using Skynet.Core.ResourceProvider;

namespace Skynet.Core.Materialization;

// Serializer für IJsonResource (UTF-8, z. B. "application/json")
public interface IJsonResourceSerializer : IResourceSerializer<IJsonResource> { }
