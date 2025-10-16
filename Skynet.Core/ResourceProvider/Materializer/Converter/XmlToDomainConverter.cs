// C#
// Skynet.Core.Materialization/Converters/IXmlToDomainConverter.cs
namespace Skynet.Core.ResourceProvider;

// XML -> Domänentyp TDomain
public interface IXmlToDomainConverter<TDomain> : IResourceConverter<IXmlResource, TDomain> { }
