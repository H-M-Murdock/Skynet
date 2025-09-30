# Bootstrap-Phase – Übersicht

In der Bootstrap-Phase werden nur minimale, robuste Dienste gestartet, um frühe Diagnostik und Grundfunktionen sicherzustellen. Erst danach folgen Core/Infra/Running.

## Ziele der Bootstrap-Phase

- Frühes, plattformneutrales Logging (Console) + vorbereitete Umschaltung auf File-Logging
- Stabile Zeitbasis über IClock (SystemClock)
- Basiskonfiguration laden (ENV + optional bootstrap/appsettings.bootstrap.json)
- Temporärer Speicher nutzbar (OS-Temp mit dediziertem Unterordner)
- Lifecycle-/Fehler-Hooks (Ctrl+C, ProcessExit, Unhandled/Unobserved Exceptions)
- Versionsinformationen der Anwendung (Assembly-Versionen, optional Buildzeitpunkt)

## Implementierte Steps (Beispiele)

- BootstrapLoggingStep
  - Console-Logging aktivieren
  - Bootstrap-Verzeichnis anlegen (./bootstrap, falls konfiguriert)
  - Schreibprobe (Probe-Datei) für späteres File-Logging
  - Report: “bootstrap directory writable=…, path=…”

- BootstrapLifecycleStep
  - Ctrl+C (SIGINT), ProcessExit, UnhandledException, UnobservedTaskException Hooks
  - Notfall-Logging nach Console und ./bootstrap/lifecycle.log
  - App-weites Cancellation-Handling (Quelle im DI verfügbar)

- BootstrapClockStep
  - IClock = SystemClock registrieren
  - Report: “UTC now (at step): …, Today: …”

- BootstrapConfigStep
  - Lädt optional bootstrap/appsettings.bootstrap.json und ENV (Prefix: SKYNET_)
  - IConfiguration in DI
  - Report: “config: json ‘…’ loaded | no json, envPrefix=SKYNET_”

- BootstrapTempStep
  - OS-Temp-Verzeichnis (Path.GetTempPath()) verwenden mit Unterordner (z. B. “Skynet”)
  - RW/DELETE-Probe im Temp-Ordner
  - Report: “temp usable=…, rwdel=(r,w,d), path=…”

- BootstrapVersionStep
  - Ermittelt Product-/Assembly-/File-/Informational-Version (und heuristisch Buildzeitpunkt)
  - AppVersionInfo in DI
  - Report: “version product=…, asm=…, file=…, info=…, buildUtc=…”

## Ablauf

- Schritte sind in Barrieren (BarrierBootStep) gruppiert
- Vor/Nach jedem Step wird geloggt; Steps liefern einen kurzen Report
- Nach jeder Barriere wird ein ServiceProvider gebaut; ab diesem Zeitpunkt steht DI-Logging bereit
- Bei Fehlern: Fallback-Logging in Console und ./bootstrap/error/lifecycle.log

## Ergebnis

Nach Abschluss der Bootstrap-Phase:
- Zeit, Config, Temp, rudimentäres Logging und Lifecycle sind einsatzbereit
- Diagnostik und Reports liegen vor (Konsole + Bootstrap-Verzeichnis)
- System ist bereit für Core-/Infra-Initialisierung (TenantContext, Provider, etc.)
