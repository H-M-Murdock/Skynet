// Skynet.Core/ResourceProvider/ResourceLocator.cs
namespace Skynet.Core.ResourceProvider;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Localization;
using Tenant;

/// <summary>
/// Orchestriert Ressourcensuchen (READ) über eine geordnete Menge von IResourceProvider
/// entlang der vom ITenantContext vorgegebenen Tenant-Resolution-Chain und
/// optional Ressourcen-Mutationen (WRITE/DELETE) über registrierte IResourceWriter.
/// </summary>
public sealed class ResourceLocator : IResourceLocator
{
    private readonly ImmutableArray<IResourceReader> _readProviders;
    private readonly ImmutableArray<IResourceWriter> _writeProviders;
    private readonly ITenantContext _tenantContext;
    private readonly ICultureThreadScopeFactory? _cultureScopeFactory;

    /// <summary>
    /// Erstellt einen ResourceLocator.
    /// providers: Read-Provider; Reihenfolge wird nach Priority sortiert (kleiner = früher).
    /// writers: Write-Provider; optional. Ebenfalls nach Priority sortiert, sofern sie CanHandle unterstützen.
    /// tenantContext: optional; wenn null, ProgramTenantContext.Instance als Fallback.
    /// cultureScopeFactory: optional; setzt (bei READ) einen Culture-Scope (Default oder Override).
    /// </summary>
    public ResourceLocator(
        IEnumerable<IResourceReader> providers,
        IEnumerable<IResourceWriter>? writers = null,
        ITenantContext? tenantContext = null,
        ICultureThreadScopeFactory? cultureScopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _readProviders = [..providers.OrderBy(p => p.Priority)];
        _writeProviders = writers is null ? ImmutableArray<IResourceWriter>.Empty : [..writers];
        _tenantContext = tenantContext ?? ProgramTenantContext.Instance;
        _cultureScopeFactory = cultureScopeFactory;
    }

    // -------------------- READ --------------------

    public Task<IResourceResult> GetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => GetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CancellationToken cancellationToken = default)
        => TryGetAsync(request, new ResourceQueryOptions(), cancellationToken);

    public Task<ResourceLookupResult> TryGetAsync(ResourceRequest request, CultureInfo? culture, CancellationToken cancellationToken = default)
        => TryGetAsync(request, new ResourceQueryOptions(culture), cancellationToken);

    public async Task<IResourceResult> GetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var res = await TryGetAsync(request, options, cancellationToken).ConfigureAwait(false);
        
        if (res.Status is ResourceLookupStatus.Found or ResourceLookupStatus.NotModified)
            return res.Resource!;

        // Fehlerbehandlung verfeinert
        if (res.Status == ResourceLookupStatus.Forbidden)
            throw new UnauthorizedAccessException($"Zugriff auf Ressource '{request.Key}' verweigert: {res.Reason}");

        if (res.Status == ResourceLookupStatus.Error)
            throw new InvalidOperationException($"Fehler beim Abruf von Ressource '{request.Key}': {res.Reason}");

        // Fallback: NotFound
        var chain = string.Join(" -> ", _tenantContext.ResolutionChain.Select(t => t.ToString()));
        var providerList = _readProviders.Length == 0
            ? "(keine Provider registriert)"
            : string.Join(", ", _readProviders.Select(p => p.Id.ToString()));

        var reasonPart = string.IsNullOrWhiteSpace(res.Reason)
            ? "Kein Provider hat eine Reason gemeldet."
            : $"Letzte Reason: {res.Reason}";

        var message = new StringBuilder()
            .AppendLine($"Ressource nicht gefunden für Key '{request.Key}'.")
            .AppendLine($"Tenant-Chain: [{chain}]")
            .AppendLine($"Geprüfte Provider: [{providerList}]")
            .AppendLine(reasonPart)
            .ToString();

        throw new KeyNotFoundException(message);
    }

    public async Task<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        ResourceQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        IDisposable? scope = null;
        try
        {
            if (_cultureScopeFactory is not null)
            {
                scope = options.CultureOverride is { } ci
                    ? _cultureScopeFactory.BeginScope(ci)
                    : _cultureScopeFactory.BeginScope();
            }

            string? lastReason = null;

            // KORREKTUR: Vorfilterung entfernt. 
            // Grund: Ein "GlobalProvider" ignoriert die TenantId im Request evtl. komplett (ist ihm egal),
            // er muss aber trotzdem gefragt werden, auch wenn er für den "CurrentTenant" (der spezifischste)
            // vielleicht "CanHandle" false liefern würde (unwahrscheinlich, aber möglich).
            // Wichtiger: CanHandle prüft meist Key-Pattern/ResourceKind, nicht Tenant.

            foreach (var tenant in _tenantContext.ResolutionChain)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scopedRequest = request with { TenantId = tenant };

                foreach (var provider in _readProviders)
                {
                    if (!provider.CanHandle(scopedRequest)) continue;

                    var lookup = await provider.TryGetAsync(scopedRequest, cancellationToken).ConfigureAwait(false);

                    switch (lookup.Status)
                    {
                        case ResourceLookupStatus.Found:
                        case ResourceLookupStatus.NotModified:
                            // Treffer! Hier brechen wir sofort ab (First Match Wins über Tenants & Provider hinweg)
                            return lookup;

                        case ResourceLookupStatus.Forbidden:
                            // Explizites Verbot unterbricht die Chain sofort (Security)
                            return lookup;
                            
                        case ResourceLookupStatus.Error:
                             // Fehler merken, aber vielleicht haben wir Glück bei einem anderen Provider?
                             // Strategie-Entscheidung: Fail-Fast oder Continue? 
                             // Meistens: Continue bei Error, Break bei Forbidden.
                             lastReason = lookup.Reason ?? lastReason;
                             break;

                        case ResourceLookupStatus.NotFound:
                            lastReason = lookup.Reason ?? lastReason;
                            break;
                    }
                }
            }

            return ResourceLookupResult.NotFound(lastReason);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    // -------------------- WRITE --------------------

    public async Task<IResourceWriteResult> WriteAsync(
        ResourceRequest request,
        Stream content,
        bool createIfMissing = true,
        string? ifMatch = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Auswahl: erster Writer, der diese Resource (Kind) schreibt.
        // Optional könnte IResourceWriter CanHandle besitzen; hier einfache Routing-Variante.
        var writer = SelectWriter(request);
        if (writer is null)
            throw new NotSupportedException($"Kein schreibfähiger Provider für ResourceKind '{request.ResourceType}' registriert.");

        return await writer.WriteAsync(request, content, createIfMissing, ifMatch, contentType, cancellationToken)
                           .ConfigureAwait(false);
    }
    
    public async Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        var writer = SelectWriter(request);
        if (writer is null)
            throw new NotSupportedException($"Kein schreibfähiger Provider für ResourceKind '{request.ResourceType}' registriert.");

        return await writer.DeleteAsync(request, ifMatch, cancellationToken).ConfigureAwait(false);
    }

    // Auswahlstrategie für Writer: nach Fähigkeiten/Policy und Priorität.
    private IResourceWriter? SelectWriter(ResourceRequest request)
    {
        // 1) Capabilities prüfen
        var candidates = _writeProviders
            .Where(w => w is not IResourceWriteCapabilities caps || caps.CanHandle(request))
            .ToList(); // Materialize

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        // 2) Sortieren nach Priority (Interface IResourceWriteCapabilities)
        // Writer ohne Interface bekommen Priority int.MaxValue (ganz hinten)
        var ordered = candidates.OrderBy(w => 
            w is IResourceWriteCapabilities caps ? caps.Priority : int.MaxValue);

        // 3) Hardcoded Policy (vermeidet Secrets im Filesystem, wenn Alternativen da sind)
        if (request.ResourceType == ResourceKind.Secret)
        {
             // Wenn wir Alternativen zum FileSystem haben, nimm diese.
             var nonFs = ordered.FirstOrDefault(w => w.GetType().Name != "FileSystemResourceWriter");
             if (nonFs != null) return nonFs;
        }

        return ordered.FirstOrDefault();
    }

    // -------------------- LIST --------------------

    public async Task<IResourceListResult> ListKeysAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Strategieänderung: Wir unterstützen hier KEIN echtes Paging über Provider hinweg,
        // da das Token-Management zu komplex/fehleranfällig ist.
        // Wir sammeln alle Keys von allen Tenants/Providern (Overlay) und geben sie zurück.
        // Falls der Consumer Paging braucht, muss er das Resultat filtern, oder wir
        // implementieren hier "In-Memory-Paging" auf der Gesamtmenge.
        
        cancellationToken.ThrowIfCancellationRequested();

        var allKeys = new HashSet<string>(StringComparer.Ordinal);
        
        // Hinweis: Wir iterieren hier über die Chain (spezifisch -> global).
        // Da es ein Set ist, gewinnen Keys, die wir zuerst sehen? Nein, Keys sind nur Strings.
        // Aber: Ein Key "appsettings.json", der in Tenant A existiert und in Global, 
        // erscheint im Set nur einmal. Das ist korrekt (Overlay-Effekt).

        foreach (var tenant in _tenantContext.ResolutionChain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scopedRequest = request with { TenantId = tenant };

            foreach (var provider in _readProviders)
            {
                if (!provider.CanHandle(scopedRequest)) continue;

                // Wir fordern "alles" vom Provider an (limit null).
                // Warnung: Bei riesigen Buckets (S3 mit Mio Files) ist das gefährlich.
                // Für Config/Templates ist es okay.
                try 
                {
                    var (providerKeys, _) = await provider.ListKeysAsync(
                        scopedRequest, 
                        continuationToken: null, 
                        limit: null, 
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    foreach (var k in providerKeys)
                    {
                        allKeys.Add(k);
                    }
                }
                catch (NotSupportedException) 
                {
                    // Provider unterstützt kein Listing -> ignorieren
                }
            }
        }

        var sortedKeys = allKeys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        // Rückgabe ohne ContinuationToken, da wir alles geholt haben.
        return new ResourceListResult(request, sortedKeys, continuationToken: null, providerId: null);
    }
}