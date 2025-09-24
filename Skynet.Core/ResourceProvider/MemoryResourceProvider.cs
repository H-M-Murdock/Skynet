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
/// Simple in-memory provider for bootstrapping and tests.
/// Stores resources in a concurrent dictionary keyed by (TenantId, Key).
/// </summary>
public sealed class MemoryResourceProvider : IResourceProvider
{
    private sealed record Entry(
        byte[] Bytes,
        string? ContentType,
        DateTimeOffset LastModified,
        string Version);

    private readonly ConcurrentDictionary<(TenantId TenantId, string Key), Entry> _store = new();

    public bool CanHandle(ResourceRequest request) => true; // Handles all keys

    public Task<(bool found, IResourceResult? result)> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_store.TryGetValue((request.TenantId, request.Key), out var e))
        {
            var ms = new MemoryStream(e.Bytes, writable: false);
            IResourceResult result = new ResourceResult(
                tenantId: request.TenantId,
                key: request.Key,
                content: ms,
                contentType: e.ContentType,
                lastModified: e.LastModified,
                contentLength: e.Bytes.LongLength,
                version: e.Version
            );
            return Task.FromResult((true, result));
        }

        return Task.FromResult((false, (IResourceResult?)null));
    }

    // Helpers for populating the provider -----------------

    public void PutText(TenantId tenantId, string key, string text, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _store[(tenantId, key)] = new Entry(bytes, contentType, DateTimeOffset.UtcNow, ComputeHash(bytes));
    }

    public void PutBytes(TenantId tenantId, string key, byte[] data, string? contentType = null)
    {
        _store[(tenantId, key)] = new Entry(data, contentType, DateTimeOffset.UtcNow, ComputeHash(data));
    }

    private static string ComputeHash(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data));
    }
}
