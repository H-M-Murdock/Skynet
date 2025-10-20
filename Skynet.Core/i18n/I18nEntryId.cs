namespace Skynet.Core.i18n;

/// <summary>
/// Physische Identität einer konkreten Template-Variante (Tenant × Kultur × Key).
/// Verwendung:
/// - Persistenz/Audit/Export: Eindeutige Identifizierung einer gespeicherten Variante.
/// - UI/Backoffice: Nachvollziehbarkeit, Quelle, Versionierung.
/// Bestandteile:
/// - Id: Technische, stabile ID dieser konkreten Variante (z. B. Guid/GuidV7). Unabhängig von Key/Tenant/Kultur-Strings.
/// - Key: Logischer Schlüssel (Domain+Name), um die Variante einem semantischen Eintrag zuzuordnen.
/// - TenantScope: String-Repräsentation des Gültigkeitsbereichs (z. B. "tenant/{id}" oder "system").
///   Warum String? Ermöglicht flexible Backends (Files, DB, Blob-Keys) ohne TenantId-Typ abhängiger Serialisierung.
/// - Culture: IETF-Tags als String (z. B. "de-CH", "en", "fr-FR") zur robusten Persistenz/Übertragung.
/// Hinweise:
/// - Für Laufzeit-APIs im Code wird typischerweise nur I18nKey benötigt.
/// - I18nEntryId kommt ins Spiel, wenn es um Export/Import, Auditing, Diffs oder Optimistic Concurrency geht.
/// - Version/LastModified gehören in die Ressourcendaten (z. B. I18nTemplate), nicht in die Identität.
/// </summary>
public sealed record I18NEntryId(
    Guid Id,
    I18NKey Key,
    string TenantScope,
    string Culture);
