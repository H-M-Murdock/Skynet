namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Wandelt ein XML-DOM in ein typisiertes Domänenobjekt TDomain.
/// </summary>
public interface IXmlToDomainConverter<TDomain> : IResourceConverter<IXmlResource, TDomain> { }