namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Basiskontrakt für jede Art von Ressource.
/// Kapselt Metadaten (Key, Type) und den Zugriff auf den Inhalt als Stream.
/// </summary>
public interface IRawResource : IDisposable
{
    /// <summary>Logischer Schlüssel (tenant-neutral).</summary>
    string LogicalKey { get; }

    /// <summary>MIME-Type (kann null sein).</summary>
    string? ContentType { get; }

    /// <summary>
    /// Der Inhalt als Stream. 
    /// HINWEIS: Bei In-Memory-Ressourcen (IStringResource etc.) erzeugt dieser Getter oft 
    /// einen neuen MemoryStream-Wrapper um den internen Puffer.
    /// </summary>
    Stream Content { get; }
}