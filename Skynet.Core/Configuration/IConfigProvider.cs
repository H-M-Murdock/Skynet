// Skynet.Core.Configuration/IConfigProvider.cs
using Microsoft.Extensions.Primitives;
using Skynet.Core.Tenant;

namespace Skynet.Core.Configuration;

public interface IConfigProvider
{
    // Stark typisiert
    T? Get<T>(string key, ITenantContext tenantContext);

    // Rohwert (String)
    string? Get(string key, ITenantContext tenantContext);

    bool TryGet<T>(string key, ITenantContext tenantContext, out T? value);

    // Änderungen beobachten
    IChangeToken Watch(string key, ITenantContext tenantContext);

    // Keys auflisten (optional)
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix, ITenantContext tenantContext, CancellationToken ct = default);
}
