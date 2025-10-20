namespace Skynet.Core.i18n;

/// <summary>
/// Repräsentiert den Inhalt eines lokalisierbaren Templates für eine konkrete Variante (TenantScope × Kultur).
/// Zweck:
/// - Bereitstellen des Rohtexts mit Platzhaltern für das Rendering.
/// - Transport relevanter Metadaten (ContentType, Version/ETag, LastModified, Source) für Caching, Audits und Replikation.
/// Felder:
/// - Key: Logischer Schlüssel (Domain+Name). Identifiziert den semantischen Eintrag.
/// - Template: Rohtext mit Platzhaltern (z. B. "Order {OrderId} created").
/// - ContentType: Optionaler MIME-Typ ("text/plain", "text/markdown"); beeinflusst ggf. Renderer/Anzeige.
/// - Source: Herkunft (z. B. "system", "tenant", "import", "codegen") – hilfreich für Debug und Governance.
/// - Version: Optionale ETag/Versionsangabe für optimistisches Locking und Cache-Kohärenz.
/// - LastModified: Zeitstempel für Cache-Invalidierung, Replikation, Audits.
/// Richtlinien:
/// - Placeholders konsistent zwischen Kulturvarianten eines Keys halten (Validator kann das prüfen).
/// - Template enthält keine Kultur-/Tenant-Infos; diese werden durch die Auflösung (Policy) bestimmt.
/// - Für die eindeutige Persistenzadressierung kann zusätzlich I18nEntryId genutzt werden (z. B. in Repositories/DTOs).
/// </summary>
public sealed class I18NTemplate
{
    /// <summary>Logischer Schlüssel (Domain + Name) des Eintrags.</summary>
    public I18NKey Key { get; init; } = default!;

    /// <summary>Rohes Template mit Platzhaltern. Sollte nie null sein; Leerstring ist erlaubt.</summary>
    public string Template { get; init; } = string.Empty;

    /// <summary>Optionaler MIME-Typ (Standard: "text/plain"). Renderer/Viewer können darauf reagieren.</summary>
    public string? ContentType { get; init; }

    /// <summary>Herkunft der Variante (z. B. "system", "tenant", "import"). Nur informativ.</summary>
    public string Source { get; init; } = "unknown";

    /// <summary>Optionale Version/ETag für optimistisches Locking und Cache-Steuerung.</summary>
    public string? Version { get; init; }

    /// <summary>Optionale Änderungszeit für Cache-Invalidierung, Replikation und Audits.</summary>
    public DateTimeOffset? LastModified { get; init; }
}
