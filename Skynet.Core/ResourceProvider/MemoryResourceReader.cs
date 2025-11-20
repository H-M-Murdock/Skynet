// Skynet.Core/ResourceProvider/MemoryResourceReader.cs
namespace Skynet.Core.ResourceProvider;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic; // Wichtig für IReadOnlyList
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Tenant;

/// <summary>
/// Einfacher In-Memory-Provider für Tests und Bootstrapping.
/// Speichert Ressourcen pro (TenantId, Key) mit Metadaten und SHA-256 Version.
/// </summary>
public sealed class MemoryResourceReader : IResourceReader
{
    private sealed record Entry(
        byte[] Bytes,
        string? ContentType,
        DateTimeOffset LastModified,
        string Version);

    // Thread-sicherer Store
    private readonly ConcurrentDictionary<(TenantId TenantId, string Key), Entry> _store = new();

    // Statische ID für diesen Typ von Provider (oder pro Instanz via Konstruktor, wenn gewünscht)
    private static readonly ProviderId StaticId = new(new Guid("2A73B3F3-E629-44AC-8153-7B4A077B9B08"));

    public MemoryResourceReader(int priority = 10)
    {
        Priority = priority;
    }

    public ProviderId Id => StaticId;
    public int Priority { get; }

    public bool CanHandle(ResourceRequest request) => true; // Nimmt alles an

    public ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_store.TryGetValue((request.TenantId, request.Key), out var e))
            return ValueTask.FromResult(ResourceLookupResult.NotFound());

        // Wir erzeugen einen NEUEN Stream über das Byte-Array.
        // ResourceResult übernimmt Ownership (Dispose), was beim MemoryStream unkritisch ist,
        // da das Byte-Array im _store davon unberührt bleibt.
        var ms = new MemoryStream(e.Bytes, writable: false);

        IResourceResult rr = new ResourceResult(
            tenantId: request.TenantId,
            key: request.Key,
            content: ms,
            contentType: e.ContentType,
            lastModified: e.LastModified,
            contentLength: e.Bytes.LongLength,
            version: e.Version,
            providerId: Id);

        return ValueTask.FromResult(ResourceLookupResult.Found(rr));
    }

    public Task<(IReadOnlyList<string> keys, string? nextContinuationToken)> ListKeysAsync(
        ResourceRequest request,
        string? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefix = request.Key ?? string.Empty;

        // 1. Filtern (Tenant & Prefix)
        var query = _store.Keys
            .Where(k => k.TenantId.Equals(request.TenantId) && k.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k.Key);

        // 2. Continuation (Paging Filter VOR dem Sortieren ist effizienter, wenn möglich, 
        // aber bei String-Vergleich muss man aufpassen. Hier filtern wir einfach auf Lexikographie.)
        if (!string.IsNullOrEmpty(continuationToken))
        {
            query = query.Where(k => string.Compare(k, continuationToken, StringComparison.Ordinal) > 0);
        }

        // 3. Sortieren & Limitieren
        var orderedQuery = query.OrderBy(k => k, StringComparer.Ordinal);
        
        var take = (limit is > 0) ? limit.Value : int.MaxValue;
        var page = orderedQuery.Take(take).ToArray();

        string? nextToken = page.Length == take ? page[^1] : null;

        return Task.FromResult(((IReadOnlyList<string>)page, nextToken));
    }

    // --- Convenience Writer (Backdoor für Tests) ---

    public void PutText(TenantId tenantId, string key, string text, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = ComputeHash(bytes);
        _store[(tenantId, key)] = new Entry(bytes, contentType, DateTimeOffset.UtcNow, hash);
    }

    public void PutBytes(TenantId tenantId, string key, byte[] data, string? contentType = null)
    {
        // Defensive Copy, falls der Aufrufer das Array später ändert
        var copy = data.ToArray(); 
        var hash = ComputeHash(copy);
        _store[(tenantId, key)] = new Entry(copy, contentType, DateTimeOffset.UtcNow, hash);
    }
    
    // Helper um schnell ganze Sets zu laden
    public void Clear() => _store.Clear();

    private static string ComputeHash(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data));
    }
}