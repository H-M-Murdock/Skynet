namespace Skynet.Core.Logging;

public interface IEnricher
{
    // Anreichern eines Events unmittelbar vor dem Encoding/Enqueue:
    // - EventId setzen, falls leer (GuidV7)
    // - CorrelationId aus aktuellem Scope/Activity hinzufügen (falls vorhanden)
    // - Tenant/Component etc. aus Context übernehmen
    // Richtlinie: Keine teuren Operationen oder IO; rein synchron und leichtgewichtig.
    void Enrich(ILogEvent evt);
}