using System.Security.Cryptography.X509Certificates;
using Skynet.Core.Licensing;

namespace Skynet.Core.Tenant;

/// <summary>
/// Definiert den typisierten Lesezugriff auf Ressourcen eines Mandanten.
/// Diese Schnittstelle abstrahiert den physischen Speicherort (Dateisystem, Datenbank, Cloud).
/// </summary>
public interface ITenantResources
{
    /// <summary>
    /// Lädt einen Konfigurationswert oder ein JSON-Dokument.
    /// </summary>
    /// <param name="key">Der logische Name der Konfiguration (ohne Dateiendung, z. B. "app" für "app.json").</param>
    /// <param name="ct">CancellationToken für asynchrone Operationen.</param>
    /// <returns>Der Inhalt als String (meist JSON) oder <c>null</c>, wenn nicht gefunden.</returns>
    Task<string?> ConfigValueAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Lädt ein Geheimnis (z. B. API-Keys, ConnectionStrings).
    /// Die Implementierung sollte sicherstellen, dass verschlüsselte Werte hier entschlüsselt zurückgegeben werden.
    /// </summary>
    /// <param name="key">Der logische Name des Secrets (ohne Dateiendung, z. B. "database").</param>
    /// <returns>Das entschlüsselte Geheimnis als String oder <c>null</c>.</returns>
    Task<string?> SecretAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Lädt Lizenzinformationen für den Tenant.
    /// </summary>
    /// <param name="key">Der Name des Lizenzmoduls (z. B. "core" oder "modules").</param>
    /// <returns>Das deserialisierte Lizenzobjekt oder <c>null</c>.</returns>
    Task<LicenseInfo?> LicenseAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Sucht nach einem X509-Zertifikat anhand eines Namens.
    /// Die Implementierung kann verschiedene Formate (.pem, .pfx, .crt) durchprobieren.
    /// </summary>
    /// <param name="name">Der logische Name des Zertifikats (z. B. "ssl" oder "signing").</param>
    /// <returns>Das geladene Zertifikat oder <c>null</c>.</returns>
    Task<X509Certificate2?> CertificateAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Lädt ein binäres Asset (Bild, PDF, Font).
    /// </summary>
    /// <param name="relativePath">Der relative Pfad inkl. Dateiendung (z. B. "branding/logo.png").</param>
    /// <returns>Ein AssetData-Objekt mit Stream und Content-Type oder <c>null</c>.</returns>
    Task<AssetData?> AssetAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Lädt ein Text-Template (z. B. E-Mail HTML, Markdown).
    /// </summary>
    /// <param name="relativePath">Der relative Pfad inkl. Dateiendung (z. B. "email/welcome.html").</param>
    /// <returns>Der Template-Text oder <c>null</c>.</returns>
    Task<string?> TemplateTextAsync(string relativePath, CancellationToken ct = default);
}