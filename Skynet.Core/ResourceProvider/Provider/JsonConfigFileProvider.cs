// Skynet.Core/ResourceProvider/JsonConfigFileProvider.cs

namespace Skynet.Core.ResourceProvider;

public sealed class JsonConfigFileProvider : IResourceProvider
{
    private readonly string _rootFull;

    private static readonly ProviderId StaticId = new(new Guid("7BC5F7C1-2C2C-4A8B-8B1E-7C5C9E6B9F21"));
    public ProviderId Id => StaticId;
    public int Priority => 49;

    public JsonConfigFileProvider(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _rootFull = Path.GetFullPath(root);
    }

    public bool CanHandle(ResourceRequest request)
        => request.ResourceType == ResourceKind.Config && !string.IsNullOrWhiteSpace(request.Key);

    public async ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
            return ResourceLookupResult.NotFound("Unsupported resource type or empty key.");

        string fullPath;
        try
        {
            var tenant = request.TenantId.ToString();
            // {root}/{tenant}/config/{key}.json (Erweiterung optional ergänzen)
            var key = request.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? request.Key : request.Key + ".json";
            fullPath = IoUtilities.BuildSafeFullPath(_rootFull, tenant, key, subFolder: "config");
        }
        catch (Exception ex)
        {
            return ResourceLookupResult.NotFound(ex.Message);
        }

        if (!File.Exists(fullPath))
            return ResourceLookupResult.NotFound($"Config not found: {fullPath}");

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
