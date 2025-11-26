namespace Skynet.Core.i18n;

/// <summary>
/// Repräsentiert den Inhalt eines lokalisierbaren Templates für eine konkrete Variante (TenantScope × Kultur).
/// <para>
/// Trennung:
/// - <see cref="I18NEntryId"/> definiert *wer* es ist (Identität).
/// - <see cref="I18NTemplate"/> definiert *was* es ist (Inhalt).
/// </para>
/// <para>
/// Verwendung:
/// - Transportiert den Rohtext und Metadaten zwischen Repository, Cache und Renderer.
/// - Enthält keine Logik zur Auflösung (keine Fallbacks hier).
/// </para>
/// </summary>
public sealed record I18NTemplate
{
    /// <summary>
    /// Logischer Schlüssel (Domain + Name). Identifiziert den semantischen Eintrag.
    /// </summary>
    public required I18NKey Key { get; init; }

    /// <summary>
    /// Rohes Template mit Platzhaltern (z. B. "Hallo {Name}").
    /// </summary>
    public required string Template { get; init; }

    /// <summary>
    /// Optionaler MIME-Typ (z. B. "text/plain", "text/markdown", "text/html").
    /// Wenn null, wird üblicherweise "text/plain" angenommen.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Herkunft der Variante (z. B. "system", "tenant/123", "import-job-x"). 
    /// Dient dem Debugging und Governance, nicht der Programmlogik.
    /// </summary>
    public string Source { get; init; } = "unknown";

    /// <summary>
    /// Version oder ETag (z. B. Hash des Inhalts) für optimistisches Locking und Caching.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Zeitstempel der letzten Änderung. Relevant für Cache-Invalidierung.
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }
    
    /// <summary>
    /// Erstellt eine Kopie des Templates mit aktualisierter Version und Zeitstempel.
    /// Nützlich bei Updates.
    /// </summary>
    public I18NTemplate WithUpdate(string newTemplate, string newVersion, DateTimeOffset modifiedAt)
    {
        return this with 
        { 
            Template = newTemplate, 
            Version = newVersion, 
            LastModified = modifiedAt 
        };
    }
}
