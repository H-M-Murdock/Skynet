// Skynet.Core/ResourceProvider/EnvironmentResourceReader.cs
using System.Security.Cryptography;
using System.Text;
using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public enum EnvScope { Process, User, Machine }

public sealed class EnvironmentResourceReader : IResourceReader
{
    public ProviderId Id { get; }
    public int Priority { get; }

    private readonly string _appPrefix;
    private readonly EnvScope _scope;
    
    // Delegates für Testbarkeit
    private readonly Func<string, EnvScope, string?> _envGetter;
    private readonly Func<EnvScope, IEnumerable<string>> _envLister;

    /// <summary>
    /// Erstellt einen neuen Environment-Reader.
    /// </summary>
    /// <param name="scope">Der Scope der Umgebungsvariablen (Process, User, Machine).</param>
    /// <param name="priority">Die Priorität im ResourceLocator.</param>
    /// <param name="appPrefix">Globaler App-Prefix (z.B. "SKYNET").</param>
    public EnvironmentResourceReader(EnvScope scope, int priority, string appPrefix = "SKYNET")
        : this(scope, priority, appPrefix, null, null)
    {
        // Delegiert an den internen Konstruktor
    }

    /// <summary>
    /// Interner Konstruktor für Tests (ermöglicht Mocking von Environment-Zugriffen).
    /// Benötigt [InternalsVisibleTo] im AssemblyInfo.
    /// </summary>
    internal EnvironmentResourceReader(
        EnvScope scope, 
        int priority, 
        string appPrefix,
        Func<string, EnvScope, string?>? envGetter,         
        Func<EnvScope, IEnumerable<string>>? envLister)     
    {
        _scope = scope;
        Priority = priority;
        _appPrefix = string.IsNullOrWhiteSpace(appPrefix) ? "SKYNET" : appPrefix.ToUpperInvariant();
        Id = GenerateId(_scope, _appPrefix);

        // Defaults setzen (System.Environment Wrapper)
        _envGetter = envGetter ?? GetEnvSystem;
        _envLister = envLister ?? GetAllEnvNamesSystem;
    }

    public bool CanHandle(ResourceRequest request)
    {
        return request.ResourceType.IsTextBased() || request.ResourceType == ResourceKind.Secret;
    }

    public ValueTask<ResourceLookupResult> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
            return ValueTask.FromResult(ResourceLookupResult.NotFound("Unsupported resource type."));

        var tenant = request.TenantId.ToString().ToUpperInvariant();
        var key = NormalizeKey(request.Key);
        var envKey = BuildEnvKey(_appPrefix, request.ResourceType, tenant, key);

        // Nutzung des Delegates (Testbar)
        var value = _envGetter(envKey, _scope);
        
        if (string.IsNullOrEmpty(value))
            return ValueTask.FromResult(ResourceLookupResult.NotFound($"ENV not found: {envKey} ({_scope})"));

        var bytes = Encoding.UTF8.GetBytes(value);
        var etag = ComputeHash(bytes);
        var stream = new MemoryStream(bytes, writable: false);

        var contentType = request.ResourceType.GetDefaultContentType();

        var result = new ResourceResult(
            tenantId: request.TenantId,
            key: request.Key,
            content: stream,
            contentType: contentType,
            lastModified: null, 
            contentLength: bytes.LongLength,
            version: etag,
            providerId: Id);

        return ValueTask.FromResult(ResourceLookupResult.Found(result));
    }

    public Task<(IReadOnlyList<string> keys, string? nextContinuationToken)> ListKeysAsync(
        ResourceRequest request,
        string? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenant = request.TenantId.ToString().ToUpperInvariant();
        var normalizedPrefix = NormalizeKey(request.Key ?? string.Empty);
        var envPrefix = BuildEnvPrefix(_appPrefix, request.ResourceType, tenant);

        // KORREKTUR: Nutzung des Delegates _envLister statt direktem Aufruf
        var vars = _envLister(_scope)
            .Where(n => n.StartsWith(envPrefix, StringComparison.Ordinal));

        var tails = vars
            .Select(n => n.Substring(envPrefix.Length))
            .Where(tail => tail.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            .OrderBy(tail => tail, StringComparer.Ordinal);

        var query = tails.AsEnumerable();
    
        if (!string.IsNullOrEmpty(continuationToken))
        {
            query = query.Where(k => string.Compare(k, continuationToken, StringComparison.Ordinal) > 0);
        }

        var take = (limit is > 0) ? limit.Value : int.MaxValue;
        var page = query.Take(take).ToArray();

        var keys = page.Select(DenormalizeKeyFallback).ToArray();
        string? nextToken = page.Length == take ? page[^1] : null;

        return Task.FromResult(((IReadOnlyList<string>)keys, nextToken));
    }

    // ... Private Helpers ...

    private static string NormalizeKey(string key)
    {
        var sb = new StringBuilder(key.Length);
        foreach (char c in key)
        {
            if (c == '/' || c == '\\' || c == ':' || c == '.')
                sb.Append('_');
            else
                sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    private static string DenormalizeKeyFallback(string normalized)
        => normalized.Replace('_', '/');

    private static string BuildEnvKey(string appPrefix, ResourceKind kind, string tenant, string key)
        => $"{BuildEnvPrefix(appPrefix, kind, tenant)}{key}";

    private static string BuildEnvPrefix(string appPrefix, ResourceKind kind, string tenant)
    {
        var kindStr = kind.ToString().ToUpperInvariant();
        return $"{appPrefix}_{kindStr}__{tenant}__";
    }

    // Implementierung für _envGetter Delegate
    private static string? GetEnvSystem(string name, EnvScope scope) => scope switch
    {
        EnvScope.Process => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process),
        EnvScope.User    => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User),
        EnvScope.Machine => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine),
        _ => null
    };

    // Implementierung für _envLister Delegate
    private static IEnumerable<string> GetAllEnvNamesSystem(EnvScope scope)
    {
        var target = scope switch
        {
            EnvScope.Process => EnvironmentVariableTarget.Process,
            EnvScope.User => EnvironmentVariableTarget.User,
            EnvScope.Machine => EnvironmentVariableTarget.Machine,
            _ => EnvironmentVariableTarget.Process
        };
    
        var vars = Environment.GetEnvironmentVariables(target);
        foreach (var key in vars.Keys)
        {
            if (key is string s) yield return s;
        }
    }
    
    private static string ComputeHash(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data));
    }
    
    private static ProviderId GenerateId(EnvScope scope, string prefix)
    {
        var input = $"ENV::{scope}::{prefix}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return new ProviderId(new Guid(hash.AsSpan(0, 16)));
    }
}