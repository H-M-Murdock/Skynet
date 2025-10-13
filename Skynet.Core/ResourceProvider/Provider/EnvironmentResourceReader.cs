// Skynet.Core/ResourceProvider/EnvironmentResourceProvider.cs
using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public enum EnvScope { Process, User, Machine }

public sealed class EnvironmentResourceReader : IResourceReader
{
    private static readonly ProviderId StaticId = new(new Guid("CF8A9E7B-0F7C-4E22-9F3F-2A3C7D0E6B11"));
    public ProviderId Id => StaticId;

    public int Priority { get; }

    private readonly string _appPrefix;
    private readonly EnvScope _scope;

    public EnvironmentResourceReader(EnvScope scope, int priority, string appPrefix = "SKYNET")
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

    /// <summary>
    /// Listet Keys aus den Umgebungsvariablen für den angegebenen Request:
    /// - request.Key ist das Prefix (vor Normalisierung).
    /// - TenantId und ResourceType werden in das ENV-Namensschema gemappt.
    /// Paging:
    /// - continuationToken: letzter bereits gelieferter Normalized-Key (lexikographisch), exklusiv.
    /// - limit: optionale Obergrenze.
    /// Hinweis: ENV bietet kein echtes Paging; wir simulieren Paging durch Sortierung/Take.
    /// </summary>
    public Task<(IReadOnlyList<string> keys, string? nextContinuationToken)> ListKeysAsync(
        ResourceRequest request,
        string? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Prefix-Header entsprechend dem Build-Schema, aber ohne den letzten Key-Part
        var tenant = request.TenantId.ToString().ToUpperInvariant();
        var normalizedPrefix = NormalizeKey(request.Key ?? string.Empty);
        var envPrefix = BuildEnvPrefix(_appPrefix, request.ResourceType, tenant); // z. B. SKYNET_SECRET__TENANT__
        // Wir filtern Variablennamen auf diesen envPrefix und extrahieren den Segment-Teil danach.

        var vars = GetAllEnvNames(_scope)
            .Where(n => n.StartsWith(envPrefix, StringComparison.Ordinal))
            .Select(n => new { Name = n, Tail = n.Substring(envPrefix.Length) })
            .Where(x => x.Tail.StartsWith(normalizedPrefix, StringComparison.Ordinal));

        // Tail = NORMALIZED_KEY; sortieren
        var ordered = vars.Select(x => x.Tail)
                          .OrderBy(x => x, StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(continuationToken))
        {
            ordered = ordered.Where(x => string.Compare(x, continuationToken, StringComparison.Ordinal) > 0)
                             .OrderBy(x => x, StringComparer.Ordinal);
        }

        var take = (limit is > 0) ? limit.Value : int.MaxValue;
        var tails = ordered.Take(take).ToArray();

        // Rückkonvertierung: aus NORMALIZED_KEY wieder das logische Key-Format ableiten.
        // Da NormalizeKey '/', '\' und '.' zu '_' macht, ist eine verlustfreie Rückkonvertierung nicht möglich.
        // Wir geben daher den normalisierten Key als Rückgabekey aus.
        // Wenn du eine verlustfreie Runde möchtest, definiere ein bidirektionales Mapping.
        var keys = tails.Select(t => DenormalizeKeyFallback(t)).ToArray();

        string? nextToken = tails.Length == take ? tails[^1] : null;
        return Task.FromResult(((IReadOnlyList<string>)keys, nextToken));
    }

    private static string NormalizeKey(string key)
    {
        var norm = key.Replace('/', '_').Replace('\\', '_').Replace('.', '_');
        while (norm.Contains("__")) norm = norm.Replace("__", "_");
        return norm.ToUpperInvariant();
    }

    private static string DenormalizeKeyFallback(string normalized)
        => normalized.Replace('_', '/'); // best-effort; original Separatoren sind nicht rekonstruierbar

    private static string BuildEnvKey(string appPrefix, ResourceKind kind, string tenant, string key)
        => kind switch
        {
            ResourceKind.Config   => $"{appPrefix}_CONFIG__{tenant}__{key}",
            ResourceKind.Secret   => $"{appPrefix}_SECRET__{tenant}__{key}",
            ResourceKind.Template => $"{appPrefix}_TEMPLATE__{tenant}__{key}",
            _ => $"{appPrefix}_UNKNOWN__{tenant}__{key}"
        };

    private static string BuildEnvPrefix(string appPrefix, ResourceKind kind, string tenant)
        => kind switch
        {
            ResourceKind.Config   => $"{appPrefix}_CONFIG__{tenant}__",
            ResourceKind.Secret   => $"{appPrefix}_SECRET__{tenant}__",
            ResourceKind.Template => $"{appPrefix}_TEMPLATE__{tenant}__",
            _ => $"{appPrefix}_UNKNOWN__{tenant}__"
        };

    private static string? GetEnv(string name, EnvScope scope) => scope switch
    {
        EnvScope.Process => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process),
        EnvScope.User    => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User),
        EnvScope.Machine => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine),
        _ => null
    };

    private static IEnumerable<string> GetAllEnvNames(EnvScope scope) => scope switch
    {
        EnvScope.Process => Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process).Keys.Cast<string>(),
        EnvScope.User    => Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User).Keys.Cast<string>(),
        EnvScope.Machine => Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine).Keys.Cast<string>(),
        _ => Array.Empty<string>()
    };

    private static async Task<string> ComputeSha256HexAsync(byte[] data, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(new MemoryStream(data, writable: false), ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}