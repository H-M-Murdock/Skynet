// Event-Model: Repräsentiert eine einzelne Log-Zeile (ein Event).
// Ziel: Transport- und Speicher-agnostisch, stabil versionierbar.
// Wichtige Felder: EventId (GuidV7), CorrelationId (Scope/Trace), Tenant/Component im Properties-Bag.
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

/// <summary>
/// Minimalistisches, .NET-nahes Log-Event:
/// - LogLevel (Enum) für effiziente Filter/Router; LevelString als berechnete Anzeige.
/// - EventId: Microsoft.Extensions.Logging.EventId (Id/Name) für Interop mit MEL.
/// - GlobalEventId: globale, zeit-sortierbare ID (z. B. GuidV7 "n") für Eindeutigkeit/Korrelation über Systeme.
/// - State: KeyValuePair-Liste (MEL-kompatibel) statt Dictionary; flach gehalten.
/// - Tracing: TraceId/SpanId aus System.Diagnostics.Activity zur Korrelation mit verteiltem Tracing.
/// - ExceptionObj für In-Proc, Exception (string) für Transport (Encoder kann aus Obj ableiten).
/// - CategoryName (ILogger categoryName) als Quelle.
/// </summary>
public interface ILogEvent
{
    // Zeitstempel (UTC empfohlen).
    DateTimeOffset Timestamp { get; }

    // .NET LogLevel + berechnete Anzeige.
    LogLevel Level { get; }
    string LevelString { get; }

    // .NET EventId (Id/Name) – interoperabel mit Microsoft.Extensions.Logging.
    EventId EventId { get; }

    // Globale, ZEIT-SORTIERBARE ID (z. B. Guid.CreateVersion7().ToString("n")).
    string GlobalEventId { get; }

    // Kategorie/Quelle (ILogger categoryName).
    string? CategoryName { get; }

    // Optionaler Operationsname (z. B. "OrderService.Create").
    string? Operation { get; }

    // Strukturierte Daten im MEL-Pattern (State).
    IReadOnlyList<KeyValuePair<string, object?>> State { get; }

    // Exception für In-Proc-Verarbeitung und transportierbarer Text für Encoder/Sinks.
    Exception? ExceptionObj { get; }
    string? Exception { get; }

    // Korrelation/Tracing.
    string? CorrelationId { get; }
    string? TraceId { get; }
    string? SpanId { get; }
}