// Skynet.Core/ResourceProvider/ResourceKind.cs
namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Logische Ressourcentypen.
/// </summary>
public enum ResourceKind
{
    // Sicherheitsnetz: Uninitialisierte Variablen fallen hierhin, nicht auf "File".
    Unknown = 0,
    
    File = 1,
    Config = 2,
    Certificate = 3,
    License = 4,
    Asset = 5,
    Template = 6,
    Secret = 7,
    // Sinnvolle Ergänzung für I18n-Systeme
    Localization = 8, 
    // Sinnvoll für gespeicherte Skripte (Lua, Python, JS)
    Script = 9 
}

/// <summary>
/// Zentralisiert die Policies, die zuvor nur im Kommentar standen.
/// </summary>
public static class ResourceKindExtensions
{
    /// <summary>
    /// Liefert den empfohlenen Default-MIME-Type für diesen Ressourcentyp.
    /// </summary>
    public static string GetDefaultContentType(this ResourceKind kind)
    {
        return kind switch
        {
            ResourceKind.Config       => "application/json",
            ResourceKind.License      => "application/json",
            ResourceKind.Template     => "text/plain; charset=utf-8",
            ResourceKind.Script       => "text/plain; charset=utf-8",
            ResourceKind.Localization => "application/json", // oder text/xml bei .resx
            
            // Binär-Defaults
            ResourceKind.Secret       => "application/octet-stream",
            ResourceKind.Certificate  => "application/octet-stream", // Oft pkcs12, aber octet ist sicherer Fallback
            ResourceKind.Asset        => "application/octet-stream", // Sollte via Dateiendung verfeinert werden
            ResourceKind.File         => "application/octet-stream",
            
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Gibt an, ob dieser Typ sensible Daten enthält (für Logging/Caching-Policies).
    /// </summary>
    public static bool IsSensitive(this ResourceKind kind)
    {
        return kind is ResourceKind.Secret or ResourceKind.Certificate;
    }

    /// <summary>
    /// Gibt an, ob dieser Typ üblicherweise textbasiert ist (z.B. für Editor-Anzeige).
    /// </summary>
    public static bool IsTextBased(this ResourceKind kind)
    {
        return kind is ResourceKind.Config 
                    or ResourceKind.Template 
                    or ResourceKind.License 
                    or ResourceKind.Script
                    or ResourceKind.Localization;
    }
}
