// Skynet.Core/ResourceProvider/ResourceKind.cs
namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Logische Ressourcentypen.
/// ContentType-Policy (Default-Heuristik bei fehlender expliziter Angabe):
/// - Config   → "application/json" (UTF-8 JSON-Konfiguration)
/// - Template → "text/plain; charset=utf-8" (reiner Text/Template)
/// - Secret   → "application/octet-stream" (binär; kein implizites Klartext-Format)
/// - Asset    → abhängig vom Dateinamen/Erweiterung (Heuristik), sonst "application/octet-stream"
/// - License  → "application/json"
/// - Certificate → je nach Container "application/x-pkcs12", "application/x-pem-file" o. ä. (Implementierung definiert)
/// - File     → Heuristik anhand Extension; unbekannt → "application/octet-stream"
/// Hinweis: Implementierungen dürfen diese Defaults überschreiben, sollten aber konsistent bleiben.
/// </summary>
public enum ResourceKind
{
    File = 0,
    Config = 1,
    Certificate = 2,
    License = 3,
    Asset = 4,
    Template = 5,
    Secret = 6
}
