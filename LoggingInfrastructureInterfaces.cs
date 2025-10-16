// Routing & Sinks: Entscheiden Ziel und führen IO aus (Datei-Rotation etc.)

// Abstraktion der endgültigen Schreibstelle (z. B. Datei, Console, Forwarder).
public interface ILogSink
{
    // Schreibt ein Event. Sollte intern non-blocking/gepuffert sein, um Latenz zu minimieren.
    Task WriteAsync(ILogEvent evt, CancellationToken ct);

    // Erzwingt einen Flush der internen Puffer (z. B. bei Shutdown oder Zeitintervallen).
    Task FlushAsync(CancellationToken ct);
}

// Router wählt geeigneten Sink auf Basis von Event-Daten (Tenant, Component, Level, Datum).
public interface ILogRouter
{
    ILogSink Resolve(ILogEvent evt);
}

// Repräsentiert ein konkretes Dateiziel inkl. vollständigem Pfad.
public sealed record FileTarget(string FullPath);

// Rotation/Target-Auswahl, z. B. daily oder size-basiert.
public interface IFileRotationPolicy
{
    // Erstes Ziel für aktuelles Event (z. B. anhand Path-Template).
    FileTarget ResolveTarget(ILogEvent evt, DateTimeOffset now);

    // Ob für das Ziel ein Wechsel nötig ist (Größe/Zeit).
    bool ShouldRotate(FileTarget target, long currentBytes, DateTimeOffset now);
}

// Rendert Pfad-Templates wie "logs/{tenant}/{component}/{yyyy-MM-dd}.log".
// Konvention: Properties["TenantId"], Properties["Component"] werden genutzt, falls vorhanden.
public interface IPathTemplateRenderer
{
    string Render(ILogEvent evt, DateTimeOffset now);
}
