// Skynet.Core/ResourceProvider/FileSystemResourceWriter.cs
using System.Security.Cryptography;
using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public sealed class FileSystemResourceWriter : IResourceWriter
{
    private readonly string _rootFull;
    private readonly ProviderId _providerId;

    public int Priority { get; }

    public FileSystemResourceWriter(string root, int priority)
    {
        ArgumentNullException.ThrowIfNull(root);
        _rootFull = Path.GetFullPath(root);
        Priority = priority;

        // Erzeuge eine stabile ID pro Root (Hash des normalisierten Pfads), damit Reader/Writer derselben Quelle übereinstimmen können.
        // Wenn du explizit gleiche IDs mit dem Reader willst, verwende dasselbe Verfahren auch dort.
        _providerId = new ProviderId(DeterministicGuidFromString(_rootFull));
    }

    public async Task<IResourceWriteResult> WriteAsync(
        ResourceRequest request,
        Stream content,
        bool createIfMissing = true,
        string? ifMatch = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ArgumentException("Key must be specified for file system writes.", nameof(request));

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
            throw new InvalidOperationException($"Invalid path: {ex.Message}", ex);
        }

        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var exists = File.Exists(fullPath);

        if (!exists && !createIfMissing)
            throw new IOException("Resource does not exist and createIfMissing=false.");

        if (exists && !string.IsNullOrEmpty(ifMatch))
        {
            var currentEtag = await ComputeFileHashHexAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(currentEtag, ifMatch, StringComparison.Ordinal))
                throw new IOException("ETag mismatch (ifMatch).");
        }

        var tmpPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        await using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        if (exists)
        {
            File.Delete(fullPath);
        }
        File.Move(tmpPath, fullPath);

        var (etag, fi) = await ComputeFileMetaAsync(fullPath, cancellationToken).ConfigureAwait(false);

        return new FsResourceWriteResult(
            tenantId: request.TenantId,
            key: request.Key!,
            version: etag,
            lastModified: new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero),
            contentLength: fi.Length,
            providerId: _providerId,
            contentType: contentType ?? IoUtilities.GuessContentType(fullPath));
    }

    public async Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ArgumentException("Key must be specified for file system deletes.", nameof(request));

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
            throw new InvalidOperationException($"Invalid path: {ex.Message}", ex);
        }

        if (!File.Exists(fullPath))
        {
            return new FsResourceDeleteResult(
                tenantId: request.TenantId,
                key: request.Key!,
                deleted: false,
                previousVersion: null,
                providerId: _providerId);
        }

        if (!string.IsNullOrEmpty(ifMatch))
        {
            var currentEtag = await ComputeFileHashHexAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(currentEtag, ifMatch, StringComparison.Ordinal))
                throw new IOException("ETag mismatch (ifMatch).");
        }

        var prevEtag = await ComputeFileHashHexAsync(fullPath, cancellationToken).ConfigureAwait(false);
        File.Delete(fullPath);

        return new FsResourceDeleteResult(
            tenantId: request.TenantId,
            key: request.Key!,
            deleted: true,
            previousVersion: prevEtag,
            providerId: _providerId);
    }

    private static async Task<(string etag, FileInfo fi)> ComputeFileMetaAsync(string fullPath, CancellationToken ct)
    {
        var etag = await ComputeFileHashHexAsync(fullPath, ct).ConfigureAwait(false);
        var fi = new FileInfo(fullPath);
        return (etag, fi);
    }

    private static async Task<string> ComputeFileHashHexAsync(string fullPath, CancellationToken ct)
    {
        await using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static Guid DeterministicGuidFromString(string input)
    {
        using var sha = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input.ToUpperInvariant());
        var hash = sha.ComputeHash(bytes);
        // Nimm die ersten 16 Bytes als GUID
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    private sealed class FsResourceWriteResult : IResourceWriteResult
    {
        public FsResourceWriteResult(
            TenantId tenantId,
            string key,
            string? version,
            DateTimeOffset? lastModified,
            long? contentLength,
            ProviderId? providerId,
            string? contentType)
        {
            TenantId = tenantId;
            Key = key;
            Version = version;
            LastModified = lastModified;
            ContentLength = contentLength;
            ProviderId = providerId;
            ContentType = contentType;
        }

        public TenantId TenantId { get; }
        public string Key { get; }
        public string? Version { get; }
        public DateTimeOffset? LastModified { get; }
        public long? ContentLength { get; }
        public ProviderId? ProviderId { get; }
        public string? ContentType { get; }
    }

    private sealed class FsResourceDeleteResult : IResourceDeleteResult
    {
        public FsResourceDeleteResult(
            TenantId tenantId,
            string key,
            bool deleted,
            string? previousVersion,
            ProviderId? providerId)
        {
            TenantId = tenantId;
            Key = key;
            Deleted = deleted;
            PreviousVersion = previousVersion;
            ProviderId = providerId;
        }

        public TenantId TenantId { get; }
        public string Key { get; }
        public bool Deleted { get; }
        public string? PreviousVersion { get; }
        public ProviderId? ProviderId { get; }
    }
}
