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
    // --- READ ---

    /// <summary>
    /// Ruft eine Ressource ab. Wirft eine Exception, wenn nicht gefunden.
    /// </summary>
    Task<IResourceResult> GetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ruft eine Ressource ab mit Optionen (z.B. Culture). Wirft eine Exception, wenn nicht gefunden.
    /// </summary>
    Task<IResourceResult> GetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Versucht eine Ressource abzurufen. Liefert Status 'NotFound' statt Exception.
    /// </summary>
    Task<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Versucht eine Ressource abzurufen mit Optionen (z.B. Culture).
    /// </summary>
    Task<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default);

    // --- WRITE ---

    /// <summary>
    /// Schreibt (create/update) eine Ressource über einen passenden Writer.
    /// </summary>
    /// <param name="request">Der Request mit Key und Tenant.</param>
    /// <param name="content">Der Inhalt als Stream.</param>
    /// <param name="createIfMissing">True → legt Ressource neu an, falls nicht vorhanden.</param>
    /// <param name="ifMatch">Erwartete Version/ETag für optimistisches Locking (null = keine Bedingung).</param>
    /// <param name="contentType">Optionaler MIME-Typ (z. B. "application/json").</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Metadaten des Schreibvorgangs (Version/LastModified/ProviderId).</returns>
    Task<IResourceWriteResult> WriteAsync(
        ResourceRequest request,
        Stream content,
        bool createIfMissing = true,
        string? ifMatch = null,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Löscht eine Ressource über einen passenden Writer.
    /// </summary>
    /// <param name="request">Der Request mit Key und Tenant.</param>
    /// <param name="ifMatch">Erwartete Version/ETag (null = bedingungslos).</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default);

    // --- LIST ---

    /// <summary>
    /// Listet Ressourcenschlüssel. 
    /// Die Implementierung aggregiert/merged über Provider (inkl. Tenant-Chain).
    /// </summary>
    Task<IResourceListResult> ListKeysAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);
}