// C#
// Skynet.Core.Materialization/Converters/IJsonToDomainConverter.cs
namespace Skynet.Core.ResourceProvider;

// JSON -> Domänentyp TDomain
public interface IJsonToDomainConverter<TDomain> : IResourceConverter<IJsonResource, TDomain> { }
