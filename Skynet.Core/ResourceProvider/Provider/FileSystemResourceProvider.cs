// Skynet.Core/ResourceProvider/FileSystemResourceProvider.cs
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public sealed class FileSystemResourceProvider : IResourceProvider
{
    private readonly string _root;

    private static readonly ProviderId StaticId = new ProviderId(new Guid("F8E2A0C9-4C5B-4B2E-9B2A-9F4B2B5B3E11"));
    public ProviderId Id => StaticId;

    public int Priority => 90;

    public FileSystemResourceProvider(string root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public bool CanHandle(ResourceRequest request)
        => request.ResourceType is ResourceKind.File or ResourceKind.Asset or ResourceKind.Template or ResourceKind.Config;

    public async ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantFolder = request.TenantId.ToString();
        var safeKey = request.Key.Replace('\\', Path.DirectorySeparatorChar)
                                 .Replace('/', Path.DirectorySeparatorChar);

        var fullPath = Path.Combine(_root, tenantFolder, safeKey);
        if (!File.Exists(fullPath))
            return ResourceLookupResult.NotFound($"File not found: {fullPath}");

        try
        {
            var fi = new FileInfo(fullPath);
            string etag;
            using (var sha = SHA256.Create())
            using (var fsHash = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = await sha.ComputeHashAsync(fsHash, cancellationToken).ConfigureAwait(false);
                etag = Convert.ToHexString(hash);
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var result = new ResourceResult(
                tenantId: request.TenantId,
                key: request.Key,
                content: stream,
                contentType: null,
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
}
