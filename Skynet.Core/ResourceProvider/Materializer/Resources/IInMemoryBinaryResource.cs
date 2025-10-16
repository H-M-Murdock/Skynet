// C#
// Skynet.Core.Resources/IInMemoryBinaryResource.cs
// Rein speicherbasierte Binärressource (kein I/O nötig), kompatibel zu IBinaryResource.
namespace Skynet.Core.ResourceProvider;

public interface IInMemoryBinaryResource : IBinaryResource
{
    // Direkter Zugriff auf den Puffer; Stream kann daraus abgeleitet werden.
    ReadOnlyMemory<byte> Buffer { get; }
}