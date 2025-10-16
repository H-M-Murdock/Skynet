// C#
// Skynet.Core.Materialization/Converters/ICsvToDomainConverter.cs
namespace Skynet.Core.ResourceProvider;

// CSV -> Domänentyp TDomain
public interface ICsvToDomainConverter<TDomain> : IResourceConverter<ICsvResource, TDomain> { }
