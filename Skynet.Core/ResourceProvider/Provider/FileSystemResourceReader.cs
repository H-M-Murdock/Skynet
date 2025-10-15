// Skynet.Core/ResourceProvider/FileSystemResourceProvider.cs

using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public sealed class FileSystemResourceReader : IResourceReader
{
    private readonly string _rootFull;

    private readonly ProviderId _providerId;
    public ProviderId Id => _providerId;

    public int Priority { get; }

    public FileSystemResourceReader(string root, int priority)
    {
        ArgumentNullException.ThrowIfNull(root);
        _rootFull = Path.GetFullPath(root);
        Priority = priority;

        // deterministische ID pro Root (muss mit Writer übereinstimmen)
        _providerId = new ProviderId(DeterministicGuidFromString(_rootFull));
    }

    public bool CanHandle(ResourceRequest request) => !string.IsNullOrEmpty(request.Key);

    public async ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath;
        try
        {
            fullPath = IoUtilities.BuildSafeFullPath(
                baseRootFull: _rootFull,
                tenantIdString: request.TenantId.ToString(),
                key: request.Key,
                subFolder: null);
        }
        catch (Exception ex)
        {
            return ResourceLookupResult.NotFound(ex.Message);
        }

        if (!File.Exists(fullPath))
            return ResourceLookupResult.NotFound($"File not found: {fullPath}");

        try
        {
            var (stream, etag, fi) = await IoUtilities.OpenReadWithHashAsync(fullPath, cancellationToken).ConfigureAwait(false);

            var result = new ResourceResult(
                tenantId: request.TenantId,
                key: request.Key,
                content: stream,
                contentType: IoUtilities.GuessContentType(fullPath),
                lastModified: fi.LastWriteTimeUtc,
                contentLength: fi.Length,
                version: etag,
                providerId: Id);

            return ResourceLookupResult.Found(result);
        }
        catch (Exception ex)
        {
            return ResourceLookupResult.NotFound(ex.Message);
        }
    }

    // ... existing code ...

    /// <summary>
    /// Listet Keys (Dateipfade relativ zum Tenant-Root) anhand Prefix (request.Key).
    /// Unterstützt optionales Paging via continuationToken (letzter Key der vorherigen Seite) und limit.
    /// </summary>
    public Task<(IReadOnlyList<string> keys, string? nextContinuationToken)> ListKeysAsync(
        ResourceRequest request,
        string? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Bestimme das Tenant-Root und das Prefix-Verzeichnis
        var tenantRoot = IoUtilities.BuildSafeFullPath(
            baseRootFull: _rootFull,
            tenantIdString: request.TenantId.ToString(),
            key: string.Empty,
            subFolder: null);

        // Prefix kann auch Unterordner enthalten. Wir bilden ein sicheres Verzeichnis + optionales Rest-Prefix.
        var prefix = request.Key ?? string.Empty;
        var (prefixDirFull, fileNamePrefix) = BuildPrefixDirectory(tenantRoot, prefix);

        if (!Directory.Exists(prefixDirFull))
        {
            // Explizit typisieren, damit TResult ableitbar ist
            return Task.FromResult<(IReadOnlyList<string> keys, string? nextContinuationToken)>((Array.Empty<string>(), null));
        }

        // Enumeriere Dateien rekursiv und filtere nach Filename-Prefix (falls vorhanden)
        var files = Directory.EnumerateFiles(prefixDirFull, "*", SearchOption.AllDirectories);

        // Berechne Keys relativ zum Tenant-Root (Unix-Separator konsistent)
        var relKeys = files
            .Select(f => GetRelativePathUnix(tenantRoot, f))
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal)) // robuste Sicherheit: filtere nochmals auf gesamtes Prefix
            .OrderBy(k => k, StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(continuationToken))
        {
            relKeys = relKeys.Where(k => string.Compare(k, continuationToken, StringComparison.Ordinal) > 0)
                             .OrderBy(k => k, StringComparer.Ordinal);
        }

        var take = (limit is > 0) ? limit.Value : int.MaxValue;
        var page = relKeys.Take(take).ToArray();

        string? nextToken = page.Length == take ? page[^1] : null;
        return Task.FromResult(((IReadOnlyList<string>)page, nextToken));
    }

    private static (string dirFull, string fileNamePrefix) BuildPrefixDirectory(string tenantRoot, string prefix)
    {
        // Trenne Verzeichnisteil und Dateiname-Prefix
        var normalized = prefix.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        var dirPart = lastSlash >= 0 ? normalized[..(lastSlash + 1)] : string.Empty;
        var namePart = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;

        var dirFull = Path.GetFullPath(Path.Combine(tenantRoot, dirPart));
        // Sicherstellen, dass dirFull noch unter tenantRoot liegt
        if (!dirFull.StartsWith(Path.GetFullPath(tenantRoot), StringComparison.Ordinal))
            throw new InvalidOperationException("Computed directory escapes tenant root.");

        return (dirFull, namePart);
    }

    private static string GetRelativePathUnix(string rootFull, string fileFull)
    {
        var root = Path.GetFullPath(rootFull).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(fileFull);
        var rel = full.StartsWith(root, StringComparison.Ordinal)
            ? full[root.Length..]
            : Path.GetFileName(full);
        return rel.Replace('\\', '/');
    }

    private static Guid DeterministicGuidFromString(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input.ToUpperInvariant());
        var hash = sha.ComputeHash(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
