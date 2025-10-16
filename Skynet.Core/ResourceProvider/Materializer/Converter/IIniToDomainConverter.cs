// C#
// Skynet.Core.Materialization/Converters/IIniToDomainConverter.cs
namespace Skynet.Core.ResourceProvider;

// INI/.env -> Domänentyp TDomain
public interface IIniToDomainConverter<TDomain> : IResourceConverter<IIniResource, TDomain> { }
