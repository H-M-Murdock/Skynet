using System.Security.Cryptography.X509Certificates;
using Skynet.Core.License;

namespace Skynet.Core.Tenant;

public interface ITenantResources
{
    Task<string?> ConfigValueAsync(string key, CancellationToken ct = default);
    Task<string?> SecretAsync(string key, CancellationToken ct = default);
    Task<LicenseInfo?> LicenseAsync(string key, CancellationToken ct = default);
    Task<X509Certificate2?> CertificateAsync(string name, CancellationToken ct = default);
    Task<AssetData?> AssetAsync(string relativePath, CancellationToken ct = default);
    Task<string?> TemplateTextAsync(string relativePath, CancellationToken ct = default);
}