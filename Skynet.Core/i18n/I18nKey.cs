namespace Skynet.Core.i18n;

/// <summary>
/// Logischer, stabiler Schlüssel eines Textbausteins – unabhängig von Tenant und Kultur.
/// Verwendung:
/// - Referenzen im Code, Tests, APIs (z. B. ILocalizationService.FormatAsync(key, ...)).
/// - Stabil über Deployments hinweg; eignet sich für Feature-Flags, Telemetrie, Diffs.
/// Struktur:
/// - Domain: thematische Gruppierung (z. B. "ui", "errors", "emails", "logs").
/// - Name:   konkret identifizierbarer Eintrag innerhalb der Domain (z. B. "Order.Created").
/// Hinweise:
/// - Keine Kultur-/Tenant-Information hier – das gehört in die Auflösungsebene.
/// - Der semantische Vertrag: Die Parameternamen im Template (z. B. {OrderId}) sollten für alle Kulturvarianten dieses Keys konsistent sein.
/// </summary>
public readonly record struct I18NKey(string Domain, string Name)
{
    public override string ToString() => $"{Domain}:{Name}";
}