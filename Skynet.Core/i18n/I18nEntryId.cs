namespace Skynet.Core.i18n;

/// <summary>
/// Physische Identität einer konkreten Template-Variante (Tenant × Kultur × Key).
/// <para>
/// Dient der eindeutigen Identifizierung bei Persistenz, Export/Import und Auditing.
/// </para>
/// </summary>
/// <param name="Id">
/// Technische, stabile ID dieser konkreten Variante (z. B. GuidV7). 
/// Unabhängig von Key/Tenant/Kultur-Strings.
/// </param>
/// <param name="Key">
/// Der logische Schlüssel (Domain + Name).
/// </param>
/// <param name="TenantScope">
/// String-Repräsentation des Gültigkeitsbereichs (z. B. "tenant/123" oder "system").
/// String ermöglicht flexible Backends ohne Abhängigkeit zu TenantId-Typen.
/// </param>
/// <param name="Culture">
/// IETF-Tag (z. B. "de-CH", "en"). Sollte für Invariant leer oder spezifisch definiert sein.
/// </param>
public sealed record I18NEntryId(
    Guid Id,
    I18NKey Key,
    string TenantScope,
    string Culture)
{
    /// <summary>
    /// Validiert, dass keine null-Werte übergeben werden.
    /// </summary>
    public string TenantScope { get; init; } = TenantScope ?? throw new ArgumentNullException(nameof(TenantScope));

    /// <summary>
    /// Validiert, dass keine null-Werte übergeben werden (Leerer String ist erlaubt für Invariant).
    /// </summary>
    public string Culture { get; init; } = Culture ?? throw new ArgumentNullException(nameof(Culture));

    /// <summary>
    /// Erstellt eine kompakte String-Repräsentation für Logs und Debugging.
    /// Format: "Domain:Name @ TenantScope [Culture] (Id)"
    /// </summary>
    public override string ToString()
    {
        return $"{Key} @ {TenantScope} [{Culture}] ({Id})";
    }

    /// <summary>
    /// Erstellt eine neue ID mit einer neuen, zufälligen Guid.
    /// </summary>
    public static I18NEntryId CreateNew(I18NKey key, string tenantScope, string culture)
    {
        return new I18NEntryId(Guid.NewGuid(), key, tenantScope, culture);
    }
}