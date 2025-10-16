// Enrichment & Policies: Zuständig für Ergänzungen (IDs, Scope-Daten), Redaction und Größenlimits.

namespace Skynet.Core.Logging;

public interface ISizeLimiter
{
    // Sorgt für harte Obergrenzen (Bytes/Länge/Tiefe) – Schutz gegen Log-Explosionen.
    // Beispiele: Strings kürzen, Arrays/Objekttiefe begrenzen, große Payload-Felder entfernen.
    void Truncate(ILogEvent evt);
}