// C#
// Skynet.Core.Materialization/Serializers/IXmlResourceSerializer.cs
using Skynet.Core.Tenant;
using Skynet.Core.ResourceProvider;

namespace Skynet.Core.Materialization;

// Serializer für IXmlResource (UTF-8, z. B. "application/xml")
public interface IXmlResourceSerializer : IResourceSerializer<IXmlResource> { }
