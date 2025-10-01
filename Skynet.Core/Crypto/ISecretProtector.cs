// Skynet.Core/Crypto/ISecretProtector.cs
namespace Skynet.Core.Crypto;

public interface ISecretProtector
{
    // String (UTF-8) → Base64 (Cipher)
    string Protect(string plainText);
    string Unprotect(string cipherBase64);

    // Bytes → Bytes
    byte[] Protect(byte[] plain);
    byte[] Unprotect(byte[] cipher);

    // Stream → Stream (Position am Ende; Caller ist für Dispose verantwortlich)
    Stream Protect(Stream plainStream);
    Stream Unprotect(Stream cipherStream);

    // Async-Varianten (lesen/schreiben vollständig)
    Task<string> ProtectAsync(string plainText, CancellationToken ct = default);
    Task<string> UnprotectAsync(string cipherBase64, CancellationToken ct = default);

    Task<byte[]> ProtectAsync(byte[] plain, CancellationToken ct = default);
    Task<byte[]> UnprotectAsync(byte[] cipher, CancellationToken ct = default);

    Task ProtectAsync(Stream plainStream, Stream destination, CancellationToken ct = default);
    Task UnprotectAsync(Stream cipherStream, Stream destination, CancellationToken ct = default);
}
