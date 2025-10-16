// Skynet.Core.Materialization/IResourceMaterializer.cs

using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public interface IResourceMaterializer
{
    // READ (Tenant implizit via ITenantContextAccessor.Current)
    Task<bool> TryGetAsync<T>(string logicalKey, out T? value, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string logicalKey, CancellationToken ct = default); // wirft bei Fehlern

    // READ (expliziter Tenant)
    Task<bool> TryGetAsync<T>(ITenantContext tenant, string logicalKey, out T? value, CancellationToken ct = default);
    Task<T?> GetAsync<T>(ITenantContext tenant, string logicalKey, CancellationToken ct = default);

    // WRITE (Tenant implizit)
    Task WriteAsync<T>(string logicalKey, T value, string? ifMatch = null, CancellationToken ct = default);

    // WRITE (expliziter Tenant)
    Task WriteAsync<T>(ITenantContext tenant, string logicalKey, T value, string? ifMatch = null, CancellationToken ct = default);

    // DELETE
    Task DeleteAsync(string logicalKey, string? ifMatch = null, CancellationToken ct = default);
    Task DeleteAsync(ITenantContext tenant, string logicalKey, string? ifMatch = null, CancellationToken ct = default);
}
