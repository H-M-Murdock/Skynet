// Skynet.Core/ResourceProvider/IResourceProvider.cs
namespace Skynet.Core.ResourceProvider;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Einzelner Ressourcenlieferant mit klarer Identität.
/// - Id: stabile technische Kennung (für Logging/Tracing/Diagnostik).
/// - Priority: Ausführungsreihenfolge im Locator (kleiner = zuerst).
/// - CanHandle: schneller Vorab-Check, ob der Provider eine Anfrage grundsätzlich bedienen kann.
/// - TryGetAsync: führt den eigentlichen Lookup aus und liefert ein strukturiertes Ergebnis.
/// </summary>
public interface IResourceReader
{
    /// <summary>
    /// Ausführungsreihenfolge im Locator (kleiner = höhere Priorität).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Eindeutige ID des Providers.
    /// </summary>
    ProviderId Id { get; }

    /// <summary>
    /// Prüft, ob der Provider diesen Request grundsätzlich bedienen kann (z.B. passender Key-Prefix).
    /// </summary>
    bool CanHandle(ResourceRequest request);

    /// <summary>
    /// Versucht die Ressource abzurufen.
    /// Sollte im Fehlerfall (Nicht gefunden) keine Exception werfen, sondern ResourceLookupResult.NotFound zurückgeben.
    /// </summary>
    ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Listet Ressourcenschlüssel für den angegebenen Scope.
    /// Interpretation:
    /// - request.TenantId: Tenant-Scope (keine Chain-Auswertung).
    /// - request.Key: Prefix (namespaced).
    /// - request.ResourceType: Namespace/Kind.
    /// Paging:
    /// - continuationToken: provider-spezifisch; null für erste Seite.
    /// - limit: optionale Obergrenze pro Seite.
    /// Rückgabe:
    /// - keys: gefundene Keys.
    /// - nextContinuationToken: Token für die nächste Seite oder null, wenn Ende.
    /// Hinweis: Provider, die Listing nicht unterstützen, dürfen NotSupportedException werfen.
    /// </summary>
    Task<(IReadOnlyList<string> keys, string? nextContinuationToken)> ListKeysAsync(
        ResourceRequest request,
        string? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
    
}