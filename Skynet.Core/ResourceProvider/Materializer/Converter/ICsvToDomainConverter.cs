namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt CSV-Daten in eine Liste oder ein komplexes Objekt TDomain.
/// </summary>
public interface ICsvToDomainConverter<TDomain> : IResourceConverter<ICsvResource, TDomain> { }