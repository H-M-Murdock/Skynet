namespace Skynet.Core.Logging;

public interface IEnricher
{
    // Anreichern eines Events unmittelbar vor dem Encoding/Enqueue:
    // Reihenfolge im Client-Pipeline-Vertrag: Enrich → Redact → Truncate → Encode
    // CorrelationId-Regel (verbindlich):
    // - Falls evt.CorrelationId null/leer ist:
    //   1) Wenn System.Diagnostics.Activity.Current != null, verwende Activity.Current.TraceId.ToString();
    //   2) Sonst, falls ein ambienter Scope/Context eine CorrelationId trägt, verwende diese;
    //   3) Sonst generiere eine neue GuidV7 (oder fallback Guid.NewGuid()) als string "n".
    // - EventId: Wenn leer, generiere GuidV7 als string "n".
    // - Tenant/Component aus Context übernehmen (nur leichte, synchrone Operationen).
    void Enrich(ILogEvent evt);
}