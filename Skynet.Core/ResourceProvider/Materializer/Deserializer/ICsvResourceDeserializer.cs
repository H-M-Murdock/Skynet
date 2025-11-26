// C#
// Skynet.Core.Materialization/Deserializers/ICsvResourceDeserializer.cs

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Spezialisierter Deserializer, der Rohdaten in eine strukturierte ICsvResource wandelt.
/// Erwartet typischerweise "text/csv".
/// </summary>
public interface ICsvResourceDeserializer : IResourceDeserializer<ICsvResource> { }
