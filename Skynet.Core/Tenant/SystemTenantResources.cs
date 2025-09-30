// Skynet.Core/Tenant/SystemTenantResources.cs
using System.Security.Cryptography.X509Certificates;
using Skynet.Core.License;

namespace Skynet.Core.Tenant;

/// <summary>
/// Liest System-Ressourcen direkt vom Dateisystem. Beim ersten Start wird die Struktur
/// unter ./bootstrap/{SystemTenantGuid}/ initialisiert. Existiert sie bereits, werden keine Daten verändert.
/// </summary>
public sealed class SystemTenantResources : ITenantResources
{
    // Fester System-Tenant (vom Nutzer vorgegeben)
    public static readonly Guid SystemTenantGuid = new("85B28EC9-34DB-414B-9E29-2C4AE17A6923");

    private readonly string _root;
    private readonly string _configDir;
    private readonly string _certsDir;
    private readonly string _licensesDir;
    private readonly string _secretsDir;
    private readonly string _assetsDir;
    private readonly string _templatesDir;

    public SystemTenantResources(string? bootstrapRoot = "./bootstrap")
    {
        _root = Path.Combine(bootstrapRoot ?? "./bootstrap", SystemTenantGuid.ToString());
        _configDir = Path.Combine(_root, "config");
        _certsDir = Path.Combine(_root, "certs");
        _licensesDir = Path.Combine(_root, "licenses");
        _secretsDir = Path.Combine(_root, "secrets");
        _assetsDir = Path.Combine(_root, "assets");           // z. B. assets/branding/logo.png
        _templatesDir = Path.Combine(_root, "templates");     // z. B. templates/email/welcome.html

        InitializeStructureIfMissing();
    }

    public async Task<string?> ConfigValueAsync(string key, CancellationToken ct = default)
        => await ReadTextIfExistsAsync(Path.Combine(_configDir, key + ".json"), ct).ConfigureAwait(false);

    public async Task<string?> SecretAsync(string key, CancellationToken ct = default)
        => await ReadTextIfExistsAsync(Path.Combine(_secretsDir, key + ".json"), ct).ConfigureAwait(false);

    public async Task<LicenseInfo?> LicenseAsync(string key, CancellationToken ct = default)
    {
        var path = Path.Combine(_licensesDir, key + ".lic");
        var raw = await ReadTextIfExistsAsync(path, ct).ConfigureAwait(false);
        if (raw is null) return null;

        // Minimal: nur Raw füllen; Parsing/Signaturprüfung kann später ergänzt werden
        return new LicenseInfo(raw);
    }

    public Task<X509Certificate2?> CertificateAsync(string name, CancellationToken ct = default)
    {
        // Reihenfolge: .pem, .crt, .cer, .pfx (für .pfx wäre Passwort nötig; hier vorerst nicht unterstützt)
        string[] candidates =
        {
            Path.Combine(_certsDir, name + ".pem"),
            Path.Combine(_certsDir, name + ".crt"),
            Path.Combine(_certsDir, name + ".cer"),
            Path.Combine(_certsDir, name + ".pfx")
        };

        foreach (var p in candidates)
        {
            if (!File.Exists(p)) continue;

            try
            {
                // PEM/CRT/CER: X509Certificate2 liest die meisten PEM/DER-Formate;
                // Bei PFX wäre ggf. ein Passwort notwendig (TODO: Secrets nutzen).
                var cert = new X509Certificate2(p);
                return Task.FromResult<X509Certificate2?>(cert);
            }
            catch
            {
                // Weiter zum nächsten Kandidaten
            }
        }

        return Task.FromResult<X509Certificate2?>(null);
    }

    public Task<AssetData?> AssetAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Path.Combine(_assetsDir, relativePath);
        if (!File.Exists(full)) return Task.FromResult<AssetData?>(null);

        // ContentType heuristisch leer lassen – kann später via Mapping/Provider ergänzt werden
        Stream s;
        try
        {
            s = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch
        {
            return Task.FromResult<AssetData?>(null);
        }

        var fileName = Path.GetFileName(full);
        return Task.FromResult<AssetData?>(new AssetData(Stream: s, ContentType: null, FileName: fileName));
    }

    public async Task<string?> TemplateTextAsync(string relativePath, CancellationToken ct = default)
        => await ReadTextIfExistsAsync(Path.Combine(_templatesDir, relativePath), ct).ConfigureAwait(false);

    // --- Helpers ---

    private void InitializeStructureIfMissing()
    {
        if (Directory.Exists(_root)) return;

        // Struktur neu anlegen
        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(_certsDir);
        Directory.CreateDirectory(_licensesDir);
        Directory.CreateDirectory(_secretsDir);
        Directory.CreateDirectory(_assetsDir);
        Directory.CreateDirectory(Path.Combine(_assetsDir, "branding"));
        Directory.CreateDirectory(_templatesDir);
        Directory.CreateDirectory(Path.Combine(_templatesDir, "email"));
        Directory.CreateDirectory(Path.Combine(_templatesDir, "text"));
        Directory.CreateDirectory(Path.Combine(_templatesDir, "legal"));

        // Minimal-README ablegen (nur wenn neu)
        WriteReadme(_configDir, "Place system config JSON files here (e.g., app.json).");
        WriteReadme(_certsDir, "Place system certificates here (PEM/CRT/CER/PFX).");
        WriteReadme(_licensesDir, "Place system license files here (.lic).");
        WriteReadme(_secretsDir, "Place system secrets here (JSON/encrypted).");
        WriteReadme(_assetsDir, "Place branding/assets here (e.g., branding/logo.png).");
        WriteReadme(Path.Combine(_assetsDir, "branding"), "Place branding images here (logo, favicon, etc.).");
        WriteReadme(_templatesDir, "Place templates here (email/text/legal).");
        WriteReadme(Path.Combine(_templatesDir, "email"), "Email templates (.html/.txt).");
        WriteReadme(Path.Combine(_templatesDir, "text"), "Text blocks.");
        WriteReadme(Path.Combine(_templatesDir, "legal"), "Legal documents (AGB, privacy, etc.).");
    }

    private static void WriteReadme(string dir, string line)
    {
        try
        {
            File.WriteAllText(Path.Combine(dir, "README.txt"), $"{line}{Environment.NewLine}");
        }
        catch
        {
            // ignorieren – README ist optional
        }
    }

    private static async Task<string?> ReadTextIfExistsAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs);
            return await sr.ReadToEndAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
