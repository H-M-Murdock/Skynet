// C#
// Skynet.Core.ResourceProvider/IStringResource.cs
namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Repräsentiert eine textbasierte Ressource (immer UTF-8).
/// Dient als Basis für spezifische Textformate (JSON, XML, CSV).
/// </summary>
public interface IStringResource : IInMemoryBinaryResource
{
    /// <summary>Der dekodierte Textinhalt.</summary>
    string Text { get; }
}
