// C#
// Skynet.Core.Materialization/Serializers/IIniResourceSerializer.cs
using Skynet.Core.Tenant;
using Skynet.Core.ResourceProvider;

namespace Skynet.Core.Materialization;

// Serializer für IIniResource (UTF-8, z. B. "text/plain; charset=utf-8")
public interface IIniResourceSerializer : IResourceSerializer<IIniResource> { }
