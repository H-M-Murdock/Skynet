// Skynet.Core/ResourceProvider/FileSystemResourceWriter.cs

using System.Security.Cryptography;
using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Schreibt Ressourcen in das Dateisystem.
/// Features:
/// - Atomic Writes (via Temp-File + Move/Overwrite).
/// - Optimistic Locking (ETag Check vor Überschreiben).
/// - Streaming Support (speicherschonend für große Assets).
/// - Security Policy (verweigert das Schreiben von Secrets).
/// </summary>
public sealed class FileSystemResourceWriter : IResourceWriter, IResourceWriteCapabilities
{
    private readonly string _rootFull;
    private readonly ProviderId _providerId;

    public int Priority { get; }

    public FileSystemResourceWriter(string root, int priority)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentNullException(nameof(root));
        _rootFull = Path.GetFullPath(root);
        Priority = priority;

        // ID muss mit dem Reader übereinstimmen, wenn sie denselben Root nutzen.
        _providerId = new ProviderId(DeterministicGuidFromString(_rootFull));
    }

    // Policy: FS-Writer akzeptiert standardmäßig keine Secrets.
    public bool CanHandle(ResourceRequest request)
    {
        return request.ResourceType is ResourceKind.Config
                                    or ResourceKind.Template
                                    or ResourceKind.Asset
                                    or ResourceKind.License
                                    or ResourceKind.File;
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

        if (!CanHandle(request))
            throw new NotSupportedException($"FileSystemResourceWriter: ResourceKind '{request.ResourceType}' is not allowed by policy.");

        // 1. Pfad sicher auflösen
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

        // Verzeichnis sicherstellen
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var exists = File.Exists(fullPath);

        // 2. Pre-Conditions prüfen
        if (!exists && !createIfMissing)
            throw new IOException($"Resource '{request.Key}' does not exist and createIfMissing=false.");

        if (exists && !string.IsNullOrEmpty(ifMatch))
        {
            // Optimistic Locking: Hash der bestehenden Datei prüfen
            var currentEtag = await ComputeFileHashHexAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(currentEtag, ifMatch, StringComparison.Ordinal))
                throw new IOException($"ETag mismatch. Expected: {ifMatch}, Actual: {currentEtag}");
        }

        // 3. Atomic Write Pattern:
        //    a) Schreiben in temporäre Datei (Streaming)
        //    b) Atomares Verschieben (Overwrite)
        var tmpPath = fullPath + $".tmp_{Guid.NewGuid():N}";
        
        try
        {
            // Stream kopieren
            await using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            {
                await content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            // Atomarer Replace (Move mit overwrite=true ist auf POSIX und NTFS atomar für Dateien im selben Volume)
            File.Move(tmpPath, fullPath, overwrite: true);
        }
        catch
        {
            // Cleanup bei Fehler
            try { File.Delete(tmpPath); } catch { /* best effort */ }
            throw;
        }

        // 4. Metadaten des neuen Inhalts berechnen
        var (etag, fi) = await ComputeFileMetaAsync(fullPath, cancellationToken).ConfigureAwait(false);

        // ContentType bestimmen: Explizit > Enum-Default > Extension-Guess
        var resolvedContentType = contentType 
                                  ?? request.ResourceType.GetDefaultContentType(); // Extension Method nutzen

        // Falls Enum-Default "octet-stream" lieferte, versuchen wir es nochmal über die Dateiendung zu verfeinern
        if (resolvedContentType == "application/octet-stream")
        {
            var guessed = IoUtilities.GuessContentType(fullPath);
            if (guessed != null) resolvedContentType = guessed;
        }

        return new ResourceWriteResult(
            tenantId: request.TenantId,
            key: request.Key,
            version: etag,
            lastModified: fi.LastWriteTimeUtc,
            contentLength: fi.Length,
            providerId: _providerId,
            contentType: resolvedContentType);
    }

    public async Task<IResourceDeleteResult> DeleteAsync(
        ResourceRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanHandle(request))
            throw new NotSupportedException($"ResourceKind '{request.ResourceType}' is not allowed.");

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
            // Idempotent: Nichts zu tun, gilt als gelöscht.
            return new ResourceDeleteResult(
                tenantId: request.TenantId,
                key: request.Key,
                deleted: false,
                previousVersion: null,
                providerId: _providerId);
        }

        // Locking prüfen
        string? currentEtag = null;
        if (!string.IsNullOrEmpty(ifMatch) || true) // Wir brauchen den Etag auch für 'PreviousVersion'
        {
            currentEtag = await ComputeFileHashHexAsync(fullPath, cancellationToken).ConfigureAwait(false);
            
            if (!string.IsNullOrEmpty(ifMatch) && !string.Equals(currentEtag, ifMatch, StringComparison.Ordinal))
                throw new IOException("ETag mismatch during delete.");
        }

        // Löschen
        File.Delete(fullPath);

        return new ResourceDeleteResult(
            tenantId: request.TenantId,
            key: request.Key,
            deleted: true,
            previousVersion: currentEtag,
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
        var bytes = Encoding.UTF8.GetBytes(input.ToUpperInvariant());
        var hash = sha.ComputeHash(bytes);
        return new Guid(hash.AsSpan(0, 16));
    }
}