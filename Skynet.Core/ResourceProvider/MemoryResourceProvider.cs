// Skynet.Core/ResourceProvider/MemoryResourceProvider.cs
namespace Skynet.Core.ResourceProvider;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Tenant;

/// <summary>
/// Simple memory-backed provider for tests and bootstrapping.
/// Stores resources per (TenantId, Key) with metadata and SHA-256 version.
/// </summary>
public sealed class MemoryResourceProvider : IResourceProvider
{
    private sealed record Entry(
        byte[] Bytes,
        string? ContentType,
        DateTimeOffset LastModified,
        string Version);

    private readonly ConcurrentDictionary<(TenantId TenantId, string Key), Entry> _store = new();

    public bool CanHandle(ResourceRequest request) => true; // handles all keys by design
    public ProviderId Id => new ProviderId(new Guid("2A73B3F3-E629-44AC-8153-7B4A077B9B08"));

    public ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_store.TryGetValue((request.TenantId, request.Key), out var e))
            return ValueTask.FromResult(ResourceLookupResult.NotFound());
        var ms = new MemoryStream(e.Bytes, writable: false);
        IResourceResult rr = new ResourceResult(
            tenantId: request.TenantId,
            key: request.Key,
            content: ms,
            contentType: e.ContentType,
            lastModified: e.LastModified,
            contentLength: e.Bytes.LongLength,
            version: e.Version,
            providerId: Id); // ProviderId im Result setzen

        return ValueTask.FromResult(ResourceLookupResult.Found(rr));
    }

    // --- Convenience writers (for tests/dev/bootstrap) ---

    public void PutText(TenantId tenantId, string key, string text, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _store[(tenantId, key)] = new Entry(bytes, contentType, DateTimeOffset.UtcNow, ComputeHash(bytes));
    }

    public void PutBytes(TenantId tenantId, string key, byte[] data, string? contentType = null)
    {
        // store reference as-is; if mutation is a concern, copy: data = data.ToArray();
        _store[(tenantId, key)] = new Entry(data, contentType, DateTimeOffset.UtcNow, ComputeHash(data));
    }

    private static string ComputeHash(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data));
    }
}
