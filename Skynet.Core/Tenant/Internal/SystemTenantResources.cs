// Skynet.Core/Tenant/SystemTenantResources.cs
using System.Security.Cryptography.X509Certificates;
using Skynet.Core.Licensing;
using Skynet.Core.ResourceProvider;

namespace Skynet.Core.Tenant;

/// <summary>
/// Implementiert den Zugriff auf System-Ressourcen über das abstrakte ResourceProvider-System.
/// Fungiert als Adapter zwischen der spezifischen ITenantResources-API und den generischen Materializern/Locators.
/// </summary>
public sealed class SystemTenantResources : ITenantResources
{
    // Fester System-Tenant
    public static readonly Guid SystemTenantGuid = Guid.Empty;

    private readonly IResourceMaterializer _materializer;
    private readonly IResourceLocator _locator;
    
    // Wir nutzen den ProgramTenantContext, da dieser (in der aktuellen Implementierung) 
    // genau den SystemTenant repräsentiert.
    private readonly ITenantContext _systemContext;

    public SystemTenantResources(
        IResourceMaterializer materializer,
        IResourceLocator locator)
    {
        _materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _systemContext = ProgramTenantContext.Instance;
    }

    public async Task<string?> ConfigValueAsync(string key, CancellationToken ct = default)
    {
        // Mapping: key "app" -> "config/app.json" (ResourceKind: Config)
        // Wir nutzen Materializer für Encoding-Handling und Caching
        var logicalKey = $"config/{key}.json";
        var (success, value) = await _materializer.TryGetAsync<string>(_systemContext, logicalKey, ct).ConfigureAwait(false);
        return success ? value : null;
    }

    public async Task<string?> SecretAsync(string key, CancellationToken ct = default)
    {
        // Mapping: key "db" -> "secrets/db.json" (ResourceKind: Secret)
        var logicalKey = $"secrets/{key}.json";
        var (success, value) = await _materializer.TryGetAsync<string>(_systemContext, logicalKey, ct).ConfigureAwait(false);
        return success ? value : null;
    }

    public Task<LicenseInfo?> LicenseAsync(string key, CancellationToken ct = default)
    {
        // Mapping: key "modules" -> "licenses/modules.lic" (ResourceKind: License)
        // Hier wäre ein Deserializer nötig, der .lic parst. 
        // Vorerst geben wir null zurück, bis der Deserializer existiert.
        return Task.FromResult<LicenseInfo?>(null);
    }

    public async Task<X509Certificate2?> CertificateAsync(string name, CancellationToken ct = default)
    {
        // Mapping: Iteriert über Erweiterungen, da ITenantResources keinen Typ vorgibt.
        // Wir fordern byte[] an, da X509Certificate2 direkt damit umgehen kann.
        // Das vermeidet komplexe Deserializer-Logik für Zertifikate im Materializer.

        string[] extensions = { ".pem", ".crt", ".cer", ".pfx" };

        foreach (var ext in extensions)
        {
            var logicalKey = $"certs/{name}{ext}";
            
            // Wir versuchen, Bytes zu bekommen (angenommen IResourceMaterializer unterstützt byte[] via IInMemoryBinaryResource oder direkt)
            // Falls Materializer kein byte[] liefert, nutzen wir den Locator direkt.
            // Sicherer Weg hier: Locator nutzen, um Stream zu holen -> Bytes lesen.
            
            try
            {
                var request = new ResourceRequest(_systemContext.CurrentTenantId, logicalKey, ResourceKind.Certificate);
                var lookup = await _locator.TryGetAsync(request, ct).ConfigureAwait(false);
                
                if (lookup.Status == ResourceLookupStatus.Found && lookup.Resource != null)
                {
                    await using var resource = lookup.Resource;
                    await using var ms = new MemoryStream();
                    await resource.Content.CopyToAsync(ms, ct).ConfigureAwait(false);
                    var bytes = ms.ToArray();

                    try
                    {
                        return new X509Certificate2(bytes);
                    }
                    catch
                    {
                        // Korrupte Datei oder falsches Format -> weiter zum nächsten Kandidaten
                    }
                }
            }
            catch
            {
                // Ignore errors during candidate search
            }
        }

        return null;
    }

    public async Task<AssetData?> AssetAsync(string relativePath, CancellationToken ct = default)
    {
        // Mapping: "logo.png" -> "assets/logo.png" (ResourceKind: Asset)
        var logicalKey = $"assets/{relativePath}";
        var request = new ResourceRequest(_systemContext.CurrentTenantId, logicalKey, ResourceKind.Asset);

        // Hier nutzen wir direkt den Locator, da AssetData einen Stream und Metadaten (ContentType) braucht.
        // Der Materializer gibt oft nur das POCO zurück und verschluckt Metadaten.
        var lookup = await _locator.TryGetAsync(request, ct).ConfigureAwait(false);

        if (lookup.Status == ResourceLookupStatus.Found && lookup.Resource != null)
        {
            // WICHTIG: Wir dürfen den Stream hier nicht usen/disposen, da AssetData ihn übernimmt!
            // AssetData muss Ownership klären. Üblicherweise übernimmt der Caller das Dispose des Streams im AssetData.
            // Aber: IResourceResult ist auch Disposable.
            // Lösung: Wir geben den Content-Stream weiter. Der Caller von AssetAsync muss AssetData.Stream disposen.
            // IResourceResult.Dispose() würde den Stream schließen. Wir müssen also das Result disposen, aber den Stream "detachen" oder offen lassen?
            // Da IResourceResult meist nur ein Wrapper um den Stream ist, reicht es, wenn der Stream disposed wird.
            // (Bei komplexen Providern müsste man aufpassen).
            
            // Wir geben ContentType und FileName (aus Key) weiter.
            var fileName = Path.GetFileName(relativePath);
            return new AssetData(lookup.Resource.Content, lookup.Resource.ContentType, fileName);
        }

        return null;
    }

    public async Task<string?> TemplateTextAsync(string relativePath, CancellationToken ct = default)
    {
        // Mapping: "email/welcome.html" -> "templates/email/welcome.html" (ResourceKind: Template)
        var logicalKey = $"templates/{relativePath}";
        var (success, value) = await _materializer.TryGetAsync<string>(_systemContext, logicalKey, ct).ConfigureAwait(false);
        return success ? value : null;
    }
}