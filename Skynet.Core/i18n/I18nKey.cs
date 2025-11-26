namespace Skynet.Core.i18n;

/// <summary>
/// Logischer, stabiler Schlüssel eines Textbausteins – unabhängig von Tenant und Kultur.
/// <para>
/// Struktur: "Domain:Name" (z. B. "errors:Order.NotFound")
/// </para>
/// Verwendung:
/// <list type="bullet">
/// <item>Referenzen im Code, Tests, APIs (z. B. ILocalizationService.FormatAsync(key, ...)).</item>
/// <item>Stabil über Deployments hinweg; eignet sich für Feature-Flags, Telemetrie, Diffs.</item>
/// </list>
/// <para>
/// Hinweise:
/// - Keine Kultur-/Tenant-Information hier – das gehört in die Auflösungsebene.
/// - Der semantische Vertrag: Die Parameternamen im Template (z. B. {OrderId}) sollten für alle Kulturvarianten dieses Keys konsistent sein.
/// </para>
/// </summary>
/// <param name="Domain">Thematische Gruppierung (z. B. "ui", "errors", "emails", "logs"). Darf keine Doppelpunkte enthalten.</param>
/// <param name="Name">Konkret identifizierbarer Eintrag innerhalb der Domain (z. B. "Order.Created").</param>
public readonly record struct I18NKey(string Domain, string Name)
{
    /// <summary>
    /// Gibt einen leeren Key zurück.
    /// </summary>
    public static I18NKey Empty => default;

    /// <summary>
    /// Gibt die String-Repräsentation im Format "Domain:Name" zurück.
    /// </summary>
    public override string ToString()
    {
        // Schutz gegen uninitialisierte Structs (default constructor)
        if (string.IsNullOrEmpty(Domain) && string.IsNullOrEmpty(Name))
        {
            return string.Empty;
        }
        return $"{Domain}:{Name}";
    }

    /// <summary>
    /// Erstellt einen I18NKey aus einem String im Format "Domain:Name".
    /// </summary>
    /// <exception cref="FormatException">Wenn das Format ungültig ist.</exception>
    public static I18NKey Parse(string value)
    {
        if (TryParse(value, out var result))
        {
            return result;
        }
        throw new FormatException($"Der Wert '{value}' ist kein gültiger I18NKey. Erwartetes Format: 'Domain:Name'.");
    }

    /// <summary>
    /// Versucht, einen String im Format "Domain:Name" in einen I18NKey zu konvertieren.
    /// </summary>
    public static bool TryParse(string? value, out I18NKey result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Wir splitten nur am ERSTEN Doppelpunkt. Der Name darf theoretisch weitere Doppelpunkte enthalten,
        // die Domain aber nicht (Konvention).
        var parts = value.Split(':', 2);
        if (parts.Length != 2)
            return false;

        var domain = parts[0].Trim();
        var name = parts[1].Trim();

        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(name))
            return false;

        result = new I18NKey(domain, name);
        return true;
    }
}