namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt ein JSON-DOM in ein typisiertes Domänenobjekt TDomain.
/// </summary>
public interface IJsonToDomainConverter<TDomain> : IResourceConverter<IJsonResource, TDomain> { }