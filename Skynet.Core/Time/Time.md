# Clock & Monotone Stopwatch (Feature Documentation)

## Motivation
Direktes Arbeiten mit `DateTime.Now` / `DateTime.UtcNow` erschwert Testbarkeit und Konsistenz.
Mit `IClock` wird die Zeitquelle über Dependency Injection abstrahiert.
Für Dauer-/Latenzmessungen wird eine monotone Quelle (`IStopwatch`) genutzt, unabhängig von der Systemuhr.
Zusätzlich vereinfacht `ScopedStopwatch` Messungen innerhalb eines `using`-Blocks – optional mit Logging-Integration über `ScopedStopwatchFactory`.

## Komponenten

### IClock (Namespace: Skynet.Core.Time)
Abstraktion der aktuellen Zeit (immer UTC-basiert).

csharp // C# public interface IClock { DateTime UtcNow { get; } DateOnly Today => DateOnly.FromDateTime(UtcNow); }

Implementierungen:
- `SystemClock` → produktive Uhr (`DateTime.UtcNow`)
- `FrozenClock` → fixierte Uhrzeit (deterministische Tests)
- `AdjustableClock` → manuell verstellbar (Integrationstests, „Zeitreisen“)

DI-Registrierung:
- Produktionsstandard: `services.AddSingleton<IClock, SystemClock>()`
- Test-Helfer (Extensions): `AddClockFrozen(DateTime utc)`, `AddClockAdjustable(DateTime startUtc)`

### IStopwatch (Namespace: Skynet.Core.Time)
Monotone Zeitquelle für präzise Messungen:

csharp // C# public interface IStopwatch { long GetTimestamp(); TimeSpan Elapsed(long startTimestamp, long endTimestamp); TimeSpan ElapsedSince(long startTimestamp); }

Implementierung:
- `Stopwatch` (Stopwatch-basierte Monotonic Clock)
    - nutzt `System.Diagnostics.Stopwatch.GetTimestamp()`
    - robuste Umrechnung in `TimeSpan` (inkl. Clamping gegen Overflows)

DI-Registrierung:
- `services.AddSingleton<IStopwatch, Stopwatch>()`

### ScopedStopwatch (Namespace: Skynet.Core.Time)
Einfacher Mess-Helfer für Codeblöcke via `using`. Ruft beim `Dispose` einen Callback mit der verstrichenen Zeit auf.
Anwendungsfall: leichte Messungen ohne Abhängigkeit auf Logging.

Beispiel:

csharp // C# var sw = provider.GetRequiredService(); using (new ScopedStopwatch(sw, elapsed => Console.WriteLine($"took {elapsed.TotalMilliseconds} ms"))) { // Code der gemessen wird }


Eigenschaften:
- Verwendet monotone Zeitquelle (`IStopwatch`)
- Idempotentes `Dispose` (Callback wird genau einmal aufgerufen)
- Argumentvalidierung

### ScopedStopwatchFactory (Namespace: Skynet.Core.Time)
Brücke zu Logging: erzeugt eine `ScopedStopwatch`, die beim `Dispose` ein Log-Event via `ILoggingClient` sendet.

DI-Registrierung (im Bootstrap integriert):
- `ScopedStopwatchFactory` wird in `BootstrapClockStep` als Singleton registriert und nutzt `IStopwatch` + `ILoggingClient`.

Verwendung:

csharp // C# var factory = provider.GetRequiredService(); using (factory.Start("OrderService.Process", level: "Information", properties: new() { ["key"] = 42 })) { // Arbeit } // Beim Dispose wird ein Event mit Template "Timing {Operation} took {ElapsedMs} ms" geloggt.

Event-Eigenschaften:
- MessageTemplate: `"Timing {Operation} took {ElapsedMs} ms"`
- Properties: enthält mindestens `ElapsedMs` (double, Millisekunden)
- `Operation`: der übergebene Operationsname
- Level: Standard `"Information"` (konfigurierbar)

## Best Practices
- In Produktivcode niemals direkt `DateTime.UtcNow` verwenden – stattdessen `IClock` injizieren.
- Für Messungen von Latenzen/Timeouts stets `IStopwatch` benutzen (monotone Quelle).
- Für wiederkehrende Mess-Scopes in Services/Pipelines: `ScopedStopwatchFactory` verwenden und den Operationsnamen konsistent wählen (z. B. `"{Service}.{Operation}"`).
- In Tests deterministische Uhren verwenden (`FrozenClock`, `AdjustableClock`), um Flakiness zu vermeiden.

## Tests
Abdeckung umfasst:
- `IClock` Implementierungen (System/Frozen/Adjustable) inkl. `Today`
- `IStopwatch` inkl. Edge-Cases (negative Reihenfolge, große Deltas, Zero-Delta)
- `ScopedStopwatch` (Dispose-Callback, Idempotenz, Argumentvalidierung)
- Logging-Integration via `ScopedStopwatchFactory`