// C#

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Erweiterung für binäre Ressourcen mit bekannten Metadaten.
/// </summary>
public interface IBinaryResource : IRawResource
{
    /// <summary>Länge in Bytes, falls bekannt (z. B. FileInfo.Length oder Buffer.Length).</summary>
    long? Length { get; }
    
    /// <summary>Optionaler Hash (z.B. SHA256 Hex) zur Integritätsprüfung/Caching.</summary>
    string? Hash { get; }
}