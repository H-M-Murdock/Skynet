namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Serializer für IJsonResource (schreibt JSON, UTF-8, typischerweise "application/json").
/// </summary>
public interface IJsonResourceSerializer : IResourceSerializer<IJsonResource> { }