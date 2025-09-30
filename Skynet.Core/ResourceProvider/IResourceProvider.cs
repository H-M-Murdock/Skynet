// Skynet.Core/ResourceProvider/IResourceProvider.cs
namespace Skynet.Core.ResourceProvider;

using System.Threading;
using System.Threading.Tasks;


/// <summary>
/// Einzelner Ressourcenlieferant mit klarer Identität.
/// - Id: stabile technische Kennung (für Logging/Tracing/Diagnostik).
/// - CanHandle: schneller Vorab-Check, ob der Provider eine Anfrage grundsätzlich bedienen kann.
/// - TryGetAsync: führt den eigentlichen Lookup aus und liefert ein strukturiertes Ergebnis ohne Exceptions für Nichttreffer.
/// </summary>
public interface IResourceProvider
{
    bool CanHandle(ResourceRequest request);
    ProviderId Id { get; }

    ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);
}