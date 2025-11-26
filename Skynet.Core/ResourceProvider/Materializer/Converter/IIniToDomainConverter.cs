namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt INI/Env-Daten in ein Konfigurationsobjekt TDomain.
/// </summary>
public interface IIniToDomainConverter<TDomain> : IResourceConverter<IIniResource, TDomain> { }