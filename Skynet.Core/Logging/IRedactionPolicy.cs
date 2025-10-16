namespace Skynet.Core.Logging;

public interface IRedactionPolicy
{
    // Entfernt/Maskiert sensible Daten in den Properties (PII/Secrets).
    // Implementierung sollte idempotent sein (mehrfaches Aufrufen schadet nicht).
    void Redact(IDictionary<string, object?> properties);
}