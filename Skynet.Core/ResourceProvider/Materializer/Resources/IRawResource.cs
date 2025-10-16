namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Basiskontrakt für Rohressourcen (Stream-basiert).
/// Abgeleitete, spezialisierte Typen (z. B. IJsonResource, IXmlResource, ITextResource)
/// können bequeme Zugriffe (JsonDocument, XDocument, Text) anbieten.
/// </summary>
public interface IRawResource : IDisposable
{
    /// <summary>Logischer Schlüssel (tenant-neutral; Tenant steht im Aufrufkontext).</summary>
    string LogicalKey { get; }

    /// <summary>Content-Type (kann null/ungenau sein; Parser/Converter dürfen sniffen).</summary>
    string? ContentType { get; }

    /// <summary>Inhalt als Stream. Der Consumer ist für Dispose verantwortlich.</summary>
    Stream Content { get; }
}