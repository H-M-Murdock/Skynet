// Event-Model: Repräsentiert eine einzelne Log-Zeile (ein Event).
// Ziel: Transport- und Speicher-agnostisch, stabil versionierbar.
// Wichtige Felder: EventId (GuidV7), CorrelationId (Scope/Trace), Tenant/Component im Properties-Bag.
namespace Skynet.Core.Logging;

public interface ILogEvent
{
    // Zeitpunkt des Events (UTC empfohlen). Wird i. d. R. im Erzeugungszeitpunkt gesetzt.
    DateTimeOffset Timestamp { get; }

    // Log-Level als standardisierter String: "Trace","Debug","Information","Warning","Error","Critical".
    // String statt Enum erleichtert Schema-Evolution und externe Tools.
    string Level { get; }

    // MessageTemplate nach Serilog-ähnlichem Muster, z. B. "User {UserId} created".
    // Formatierung sollte erst im Sink/Formatter erfolgen; transportiert wird Template + Properties.
    string MessageTemplate { get; }

    // Strukturiertes Daten-Bag. Muss serialisierbare Werte enthalten (Primitiv-/Record-ähnlich).
    // Empfohlen: flache Struktur + konventionelle Schlüssel (TenantId, Component, etc.).
    IReadOnlyDictionary<string, object?> Properties { get; }

    // Serialisierte Exception (vereinfacht): StackTrace/Message kombiniert.
    // Alternativ könnte ein strukturiertes Exception-Objekt verwendet werden – bewusst einfach gehalten.
    string? Exception { get; }

    // Kategorie/Quelle (z. B. Logger-Name, Typname). Erleichtert Filterung/Analyse.
    string? SourceContext { get; }

    // Optionaler Operationsname (AOP/Proxy): "OrderService.Create".
    string? Operation { get; }

    // Optional numerische EventId (für bekannte Event-Kataloge). Ergänzt die globale EventId.
    int? EventCode { get; }

    // Globale, ZEIT-SORTIERBARE, eindeutige ID pro Log-Event.
    // Empfehlung: Guid.CreateVersion7().ToString("n") – ohne Bindestriche (kompakt).
    string EventId { get; }

    // Korrelation über Prozess-/Thread-Grenzen: Trace/Request/Message. Stabil über den gesamten Scope.
    // Quelle: Activity.TraceId oder im BeginScope generiert (GuidV7), falls kein Trace vorhanden.
    string? CorrelationId { get; }
}