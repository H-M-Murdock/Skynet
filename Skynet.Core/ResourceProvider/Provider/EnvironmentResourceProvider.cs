// Skynet.Core/ResourceProvider/EnvironmentResourceProvider.cs
using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public enum EnvScope { Process, User, Machine }

public sealed class EnvironmentResourceProvider : IResourceProvider
{
    private static readonly ProviderId StaticId = new(new Guid("CF8A9E7B-0F7C-4E22-9F3F-2A3C7D0E6B11"));
    public ProviderId Id => StaticId;

    public int Priority { get; }

    private readonly string _appPrefix;
    private readonly EnvScope _scope;

    public EnvironmentResourceProvider(EnvScope scope, int priority, string appPrefix = "SKYNET")
    {
        _scope = scope;
        Priority = priority;
        _appPrefix = string.IsNullOrWhiteSpace(appPrefix) ? "SKYNET" : appPrefix.ToUpperInvariant();
    }

    public bool CanHandle(ResourceRequest request)
        => request.ResourceType is ResourceKind.Config or ResourceKind.Secret or ResourceKind.Template
           && !string.IsNullOrWhiteSpace(request.Key);

    public async ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
            return ResourceLookupResult.NotFound("Unsupported resource type or empty key.");

        var tenant = request.TenantId.ToString().ToUpperInvariant();
        var key = NormalizeKey(request.Key);
        var envKey = BuildEnvKey(_appPrefix, request.ResourceType, tenant, key);

        var value = GetEnv(envKey, _scope);
        if (string.IsNullOrEmpty(value))
            return ResourceLookupResult.NotFound($"ENV not found: {envKey} ({_scope})");

        var bytes = Encoding.UTF8.GetBytes(value);
        var etag = await ComputeSha256HexAsync(bytes, cancellationToken).ConfigureAwait(false);
        var stream = new MemoryStream(bytes, writable: false);

        var contentType = request.ResourceType switch
        {
            ResourceKind.Config => "application/json",
            ResourceKind.Secret => "text/plain; charset=utf-8",
            ResourceKind.Template => "text/plain; charset=utf-8",
            _ => "text/plain; charset=utf-8"
        };

        var result = new ResourceResult(
            tenantId: request.TenantId,
            key: request.Key,
            content: stream,
            contentType: contentType,
            lastModified: null,
            contentLength: bytes.LongLength,
            version: etag,
            providerId: Id);

        return ResourceLookupResult.Found(result);
    }

    private static string NormalizeKey(string key)
    {
        var norm = key.Replace('/', '_').Replace('\\', '_').Replace('.', '_');
        while (norm.Contains("__")) norm = norm.Replace("__", "_");
        return norm.ToUpperInvariant();
    }

    private static string BuildEnvKey(string appPrefix, ResourceKind kind, string tenant, string key)
        => kind switch
        {
            ResourceKind.Config   => $"{appPrefix}_CONFIG__{tenant}__{key}",
            ResourceKind.Secret   => $"{appPrefix}_SECRET__{tenant}__{key}",
            ResourceKind.Template => $"{appPrefix}_TEMPLATE__{tenant}__{key}",
            _ => $"{appPrefix}_UNKNOWN__{tenant}__{key}"
        };

    private static string? GetEnv(string name, EnvScope scope) => scope switch
    {
        EnvScope.Process => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process),
        EnvScope.User    => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User),
        EnvScope.Machine => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine),
        _ => null
    };

    private static async Task<string> ComputeSha256HexAsync(byte[] data, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(new MemoryStream(data, writable: false), ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}