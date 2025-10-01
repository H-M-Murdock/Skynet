// Skynet.Core/ResourceProvider/FileSystemResourceProvider.cs

using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public sealed class FileSystemResourceProvider : IResourceProvider
{
    private readonly string _rootFull;

    private static readonly ProviderId StaticId = new ProviderId(new Guid("F8E2A0C9-4C5B-4B2E-9B2A-9F4B2B5B3E11"));
    public ProviderId Id => StaticId;

    public int Priority { get; }

    public FileSystemResourceProvider(string root, int priority)
    {
        ArgumentNullException.ThrowIfNull(root);
        _rootFull = Path.GetFullPath(root);
        Priority = priority;
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
}
