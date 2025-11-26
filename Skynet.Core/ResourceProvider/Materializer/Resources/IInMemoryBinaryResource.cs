// C#
// Skynet.Core.Resources/IInMemoryBinaryResource.cs
// Rein speicherbasierte Binärressource (kein I/O nötig), kompatibel zu IBinaryResource.
namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Markiert Ressourcen, die vollständig im Arbeitsspeicher liegen.
/// Erlaubt synchronen, wahlfreien Zugriff ohne I/O-Overhead.
/// </summary>
public interface IInMemoryBinaryResource : IBinaryResource
{
    /// <summary>Der Inhalt als speicherbasierter Puffer.</summary>
    ReadOnlyMemory<byte> Buffer { get; }
}