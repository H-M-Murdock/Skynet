// Skynet.Core/ResourceProvider/IResourceLocator.cs
namespace Skynet.Core.ResourceProvider;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Zentrale Orchestrierungsschicht für Ressourcen:
/// - Read (Get/TryGet) über mehrere IResourceProvider entlang der Tenant-Chain.
/// - Write/Delete über registrierte schreibfähige Provider (IResourceWriter).
/// Konsistente Metadaten (TenantId, Key, ContentType, Version/ETag, LastModified).
/// </summary>
public interface IResourceLocator
{

    Task<IResourceResult> GetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);

    Task<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);

    Task<IResourceResult> GetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Schreibt (create/update) eine Ressource über einen passenden Writer.
    /// - createIfMissing: true → legt Ressource neu an, falls nicht vorhanden.
    /// - ifMatch: erwartete Version/ETag für optimistisches Locking (null = keine Bedingung).
    /// - contentType: optionaler MIME-Typ (z. B. "application/json").
    /// Rückgabe liefert Metadaten (Version/LastModified/ProviderId).
    /// </summary>
    Task<IResourceWriteResult> WriteAsync(
        ResourceRequest request,
        Stream content,
        bool createIfMissing = true,
        string? ifMatch = null,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Löscht eine Ressource über einen passenden Writer.
    /// - ifMatch: erwartete Version/ETag (null = bedingungslos).
    /// </summary>
    Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default);
}