// Skynet.Core/ResourceProvider/IResourceLocator.cs
namespace Skynet.Core.ResourceProvider;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Zentrale Fassade für Ressourcen-Lookups über einen oder mehrere IResourceProvider.
/// - GetAsync: erzwingt einen Treffer (wirft Ausnahme bei Nichtauffinden/Fehler).
/// - TryGetAsync: liefert detailliertes Lookup-Ergebnis (Status, ggf. Resource), ohne Ausnahmefluss.
/// Überladungen mit ResourceQueryOptions erlauben optionale Abfragehinweise (z. B. Kultur).
/// </summary>
public interface IResourceLocator
{
    Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default);
    Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default);

    Task<IResourceResult> GetAsync(ResourceRequest request, ResourceQueryOptions options, CancellationToken cancellationToken = default);
    Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, ResourceQueryOptions options, CancellationToken cancellationToken = default);
}