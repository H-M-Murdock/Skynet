# IO Utilities – Übersicht und Leitfaden

Diese Utilities kapseln sichere Datei-/Verzeichnis-Operationen mit strikter Pfadvalidierung, atomischen Writes und einfachen Helfern für Existenzprüfungen, Kopieren, Verschieben und Löschen.

## Sicherheitsprinzipien

- Pfadbildung ausschließlich über BuildSafeFullPath(...)
- Strikte Validierung:
    - Erlaubte Zeichen: A–Z a–z 0–9 _ - . /
    - Backslashes werden zu Slashes normalisiert
    - Verboten:
        - "." und ".." als Segmente
        - Führender/trailing Slash im key oder subFolder
        - Doppelte Separatoren („//“)
        - Leerzeichen oder Sonderzeichen außerhalb des erlaubten Sets
    - Ergebnis muss immer unterhalb des angegebenen baseRoot liegen (Containment-Check)
- Keine stillen Downgrades: Bei ungültigen Eingaben InvalidOperationException bzw. ArgumentNullException

## API

Alle Methoden befinden sich in Skynet.Core.IoUtilities (static).

### BuildSafeFullPath(baseRootFull, tenantIdString, key, subFolder = null)

- Zweck: Sicheren absoluten Pfad generieren unter baseRoot/tenant[/subFolder]/segments
- Validiert key und subFolder strikt, normalisiert und verhindert Path Traversal
- Ausnahmen:
    - ArgumentNullException (leere/null Parameter)
    - InvalidOperationException (ungültige Zeichen/Segmente/Root-Escape)
- Rückgabe: Vollqualifizierter, sicherer Pfad

Beispiel:

csharp var full = IoUtilities.BuildSafeFullPath(root, "tenant1", "folder/sub/file.json", "static");

### OpenReadWithHashAsync(fullPath, ct = default)

- Zweck: Datei schreibgeschützt öffnen und SHA-256 als Hex-ETag berechnen
- Rückgabe: (stream, etag, fileInfo)
- Ausnahmen: FileNotFoundException, OperationCanceledException

Beispiel:

csharp var (stream, etag, fi) = await IoUtilities.OpenReadWithHashAsync(full);


### WriteAtomicAsync(baseRoot, tenant, key, content, subFolder = null, ct = default)

- Zweck: Atomisches Schreiben:
    - schreibt zunächst in temporäre Datei
    - berechnet ETag (SHA-256)
    - ersetzt Zieldatei via Replace (race-sicher durch Zielstub)
- Rückgabe: (fullPath, etag, fileInfo)
- Ausnahmen: ArgumentNullException, InvalidOperationException (via Pfadvalidierung), OperationCanceledException
- Hinweise:
    - Leerer Inhalt erzeugt ETag „E3B0C442…“
    - Schreibgeschützte Ziel-Dateien können zu UnauthorizedAccessException/IOException führen (plattformabhängig)

Beispiel:

csharp var (path, etag, _) = await IoUtilities.WriteAtomicAsync(root, "t1", "a/b.txt", bytes);


### ExistsSafe(baseRoot, tenant, key, subFolder = null)

- Zweck: Existenz einer Datei prüfen (ohne Ausnahme bei „nicht vorhanden“)
- Rückgabe: bool
- Ausnahmen: ArgumentNullException, InvalidOperationException (via Pfadvalidierung)

Beispiel:

csharp if (IoUtilities.ExistsSafe(root, "t1", "a/b.txt")) { /* ... */ }


### DeleteSafeAsync(baseRoot, tenant, key, subFolder = null, ct = default)

- Zweck: Datei sicher löschen (ReadOnly-Flag wird bestmöglich entfernt)
- Rückgabe: true, wenn gelöscht wurde; false, wenn nicht vorhanden
- Ausnahmen: ArgumentNullException, InvalidOperationException, OperationCanceledException

Beispiel:

csharp var deleted = await IoUtilities.DeleteSafeAsync(root, "t1", "a/b.txt");


### DirectoryExistsSafe(baseRoot, tenant, key, subFolder = null)

- Zweck: Existenz eines Verzeichnisses prüfen
- Rückgabe: bool
- Ausnahmen: ArgumentNullException, InvalidOperationException

Beispiel:


csharp var ok = IoUtilities.DirectoryExistsSafe(root, "t1", "static/assets", "web");


### DirectoryDeleteSafeAsync(baseRoot, tenant, key, subFolder = null, ct = default)

- Zweck: Verzeichnis rekursiv löschen (ReadOnly-Flags von Dateien werden bestmöglich entfernt)
- Rückgabe: true, wenn gelöscht; false, wenn nicht vorhanden
- Ausnahmen: ArgumentNullException, InvalidOperationException, OperationCanceledException

Beispiel:

csharp var removed = await IoUtilities.DirectoryDeleteSafeAsync(root, "t1", "cache/tmp");


### CopySafeAsync(baseRoot, tenant, srcKey, dstKey, subFolder = null, ct = default)

- Zweck: Datei innerhalb desselben baseRoot/tenant[/subFolder] kopieren
- Zielverzeichnis wird erstellt; bestehendes Ziel wird überschrieben (overwrite: true)
- Rückgabe: (destFullPath, destInfo)
- Ausnahmen: FileNotFoundException (Quelle fehlt), ArgumentNullException/InvalidOperationException, OperationCanceledException

Beispiel:

csharp var (dst, dstInfo) = await IoUtilities.CopySafeAsync(root, "t1", "src/a.bin", "dst/a.bin");


### MoveSafeAsync(baseRoot, tenant, srcKey, dstKey, subFolder = null, ct = default)

- Zweck: Datei innerhalb desselben baseRoot/tenant[/subFolder] verschieben
- Implementierung nutzt Temp+Replace, um atomar zu wirken
- Rückgabe: (moved, destFullPath, destInfo) – moved = false, wenn Quelle fehlt
- Ausnahmen: ArgumentNullException/InvalidOperationException, OperationCanceledException

Beispiel:

csharp var (moved, dst, _) = await IoUtilities.MoveSafeAsync(root, "t1", "src/a.bin", "dst/a.bin");


## Typische Fehlermeldungen und Ursachen

- InvalidOperationException: „Invalid characters in key/subFolder“, „Invalid path segments“, „Path escapes root.“
    - Ursache: ungültige Zeichen, „.“, „..“, doppelte oder führende/trailing Separatoren, absoluter Key
- ArgumentNullException: baseRoot/tenant/key leer oder null
- FileNotFoundException: bei OpenReadWithHashAsync (fehlende Datei) oder CopySafeAsync (fehlende Quelle)
- UnauthorizedAccessException/IOException: Ziel schreibgeschützt (plattformabhängig)
- OperationCanceledException: CancellationToken wurde ausgelöst

## Best Practices

- Niemals Pfade per String-Verkettung bilden; immer BuildSafeFullPath verwenden.
- Bei Schreiboperationen atomisch schreiben (WriteAtomicAsync) und nicht direkt auf das Ziel.
- ETag konsequent nutzen, um Caching/If-None-Match zu unterstützen.
- Für Massenoperationen (Copy/Move/Delete) auf cancellation achten und Exceptions sinnvoll nach oben reichen.

## Beispiele

Minimaler Read:

csharp var full = IoUtilities.BuildSafeFullPath(root, tenantId, "assets/app.css", "static"); var (stream, etag, _) = await IoUtilities.OpenReadWithHashAsync(full, ct); // stream verwenden...

Atomischer Write:

csharp var data = Encoding.UTF8.GetBytes("{}"); var (path, etag, _) = await IoUtilities.WriteAtomicAsync(root, tenantId, "config/app.json", data, ct: ct);

Copy/Move:

csharp await IoUtilities.CopySafeAsync(root, tenantId, "inbox/file.bin", "archive/file.bin", ct: ct); await IoUtilities.MoveSafeAsync(root, tenantId, "staging/x.txt", "live/x.txt", subFolder: "static", ct: ct);

Delete/Exists:

csharp if (IoUtilities.ExistsSafe(root, tenantId, "logs/today.log")) await IoUtilities.DeleteSafeAsync(root, tenantId, "logs/today.log", ct: ct);

