// Skynet.Core/ResourceProvider/FileSystemResourceProvider.cs

using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Liest Ressourcen direkt aus dem Dateisystem.
/// Nutzt IoUtilities für strikte Sicherheit (Path Traversal Protection, Atomic Reads).
/// Struktur: {Root}/{TenantId}/{Key}.
/// </summary>
public sealed class FileSystemResourceReader : IResourceReader
{
    private readonly string _rootFull;
    private readonly ProviderId _providerId;

    public ProviderId Id => _providerId;
    public int Priority { get; }

    public FileSystemResourceReader(string root, int priority)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentNullException(nameof(root));
        
        _rootFull = Path.GetFullPath(root);
        Priority = priority;

        // Deterministische ID basierend auf dem Root-Pfad
        _providerId = new ProviderId(DeterministicGuidFromString(_rootFull));
    }

    public bool CanHandle(ResourceRequest request)
    {
        // Einfacher Check: Ist ein Key da?
        // IoUtilities prüft später strikter (Regex), aber CanHandle sollte schnell sein.
        return !string.IsNullOrEmpty(request.Key);
    }

    public async ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath;
        try
        {
            // 1. Pfad auflösen & Validieren (wirft bei ungültigen Zeichen oder ..)
            fullPath = IoUtilities.BuildSafeFullPath(
                baseRootFull: _rootFull,
                tenantIdString: request.TenantId.ToString(),
                key: request.Key,
                subFolder: null);
        }
        catch (Exception ex)
        {
            // Security-Fail oder ungültiger Key -> NotFound (mit Reason)
            return ResourceLookupResult.NotFound($"Path validation failed: {ex.Message}");
        }

        // 2. Existenz prüfen (IoUtilities.ExistsSafe wäre auch möglich, aber wir öffnen eh gleich)
        if (!File.Exists(fullPath))
            return ResourceLookupResult.NotFound("File not found on disk.");

        try
        {
            // 3. Öffnen + Hashing + Info
            var (stream, etag, fi) = await IoUtilities.OpenReadWithHashAsync(fullPath, cancellationToken).ConfigureAwait(false);

            var result = new ResourceResult(
                tenantId: request.TenantId,
                key: request.Key,
                content: stream, // ResourceResult übernimmt Dispose
                contentType: IoUtilities.GuessContentType(fullPath),
                lastModified: fi.LastWriteTimeUtc,
                contentLength: fi.Length,
                version: etag,
                providerId: Id);

            return ResourceLookupResult.Found(result);
        }
        catch (FileNotFoundException)
        {
            // Race Condition: Datei wurde zwischen File.Exists und Open gelöscht
            return ResourceLookupResult.NotFound("File disappeared.");
        }
        catch (Exception ex)
        {
            // IO-Fehler (Locking, Permissions)
            return ResourceLookupResult.Error($"IO Error: {ex.Message}");
        }
    }

    public Task<(IReadOnlyList<string> keys, string? nextContinuationToken)> ListKeysAsync(
        ResourceRequest request,
        string? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Tenant-Root ermitteln
        string tenantRoot;
        try
        {
             tenantRoot = IoUtilities.BuildSafeFullPath(
                baseRootFull: _rootFull,
                tenantIdString: request.TenantId.ToString(),
                key: "dummy", // BuildSafeFullPath braucht einen Key, wir strippen ihn gleich wieder
                subFolder: null);
             
             // Wir wollen nur den Ordner ".../TenantId/"
             tenantRoot = Path.GetDirectoryName(tenantRoot)!;
        }
        catch
        {
            // Wenn schon der Tenant-Pfad ungültig ist, gibt's auch keine Keys
            return Task.FromResult<(IReadOnlyList<string>, string?)>((Array.Empty<string>(), null));
        }

        if (!Directory.Exists(tenantRoot))
            return Task.FromResult<(IReadOnlyList<string>, string?)>((Array.Empty<string>(), null));

        // 2. Prefix vorbereiten
        var prefix = request.Key ?? string.Empty;
        
        // 3. Alle Dateien listen (bei riesigen Mengen evtl. ineffizient, aber für Configs ok)
        //    Wir nutzen EnumerateFiles, damit wir nicht alles sofort in RAM laden müssen vor dem Paging.
        var allFiles = Directory.EnumerateFiles(tenantRoot, "*", SearchOption.AllDirectories);

        var matchingKeys = allFiles
            .Select(fullPath => 
            {
                // Relativen Pfad berechnen -> das ist der logische Key
                var rel = Path.GetRelativePath(tenantRoot, fullPath);
                // Windows-Backslashes zu Slashes normalisieren
                return rel.Replace('\\', '/');
            })
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.Ordinal);

        // 4. Paging anwenden
        var query = matchingKeys.AsEnumerable();
        if (!string.IsNullOrEmpty(continuationToken))
        {
            query = query.Where(k => string.Compare(k, continuationToken, StringComparison.Ordinal) > 0);
        }

        var take = (limit is > 0) ? limit.Value : int.MaxValue;
        var page = query.Take(take).ToArray();

        string? nextToken = page.Length == take ? page[^1] : null;
        
        return Task.FromResult(((IReadOnlyList<string>)page, nextToken));
    }

    private static Guid DeterministicGuidFromString(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input.ToUpperInvariant());
        var hash = sha.ComputeHash(bytes);
        return new Guid(hash.AsSpan(0, 16));
    }
}
