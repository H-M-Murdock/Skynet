// Encoding: Wandelt ILogEvent in Bytestream und zurück. Ermöglicht NDJSON oder MessagePack flexibel.

namespace Skynet.Core.Logging;

public interface ILogEventEncoder
{
    // MIME-ähnlicher Content-Type, z. B. "application/x-ndjson" oder "application/x-msgpack".
    string ContentType { get; }

    // Schema-/Encoder-Version (z. B. "1.0"). Hilfreich für Kompatibilität und Migration.
    string Version { get; }

    // Serialisiert genau EIN Event. Für NDJSON z. B. eine einzelne JSON-Zeile ohne Newline.
    ReadOnlyMemory<byte> Encode(ILogEvent evt);

    // Optional: Decodierung einzelner Frames (z. B. Server-Test/Replay).
    // Gibt false, wenn Payload ungültig/nicht decodierbar ist.
    bool TryDecode(ReadOnlySpan<byte> payload, out ILogEvent? evt);
}