// C#
// Skynet.Core.ResourceProvider/IStringResource.cs
namespace Skynet.Core.ResourceProvider;

// UTF-8-Stringressource mit direktem Zugriff auf den Bytes-Puffer (In-Memory)
public interface IStringResource : IInMemoryBinaryResource
{
    // UTF-8-dekodierter Inhalt (keine anderen Encodings)
    string Text { get; }
}
