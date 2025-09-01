📖 Clock & DateTimeFormatter (Feature Documentation)
Motivation

Direktes Arbeiten mit DateTime.Now / DateTime.UtcNow im Code erschwert Testbarkeit und Konsistenz.

Mit IClock wird die Zeitquelle über Dependency Injection abstrahiert.

Der DateTimeFormatter nutzt diese Uhr plus Kulturinformationen, um konsistente, testbare Formatierungen zu liefern.

Kulturen werden durch LocalizationOptions gesteuert, die pro Request oder Tenant gesetzt werden können.

Components
IClock (Namespace: Skynet.Core.Time)

Abstraktion der aktuellen Zeit (immer UTC-basiert).

public interface IClock
{
DateTime UtcNow { get; }
DateOnly Today => DateOnly.FromDateTime(UtcNow);
}


Implementierungen:

SystemClock → produktive Uhr (DateTime.UtcNow)

FrozenClock → fixierte Uhrzeit (für deterministische Tests)

AdjustableClock → manuell verstellbar (für Integrationstests, Zeitreisen)

