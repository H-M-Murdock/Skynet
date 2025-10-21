// Skynet.Core/ResourceProvider/Internal/FileResourceUtil.cs

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Skynet.Core;

/// <summary>
/// IO-Hilfsfunktionen für sichere Pfadbildung und effizientes Datei-Lesen.
/// </summary>
public static class IoUtilities
{
    // A–Z a–z 0–9 _ - . /  (Slash für Segmente, Backslashes werden vorher normalisiert)
    // Keine Leerzeichen: bewusst strikt für serverseitige Ressourcenkeys.
    private static readonly Regex Allowed = new(@"^[A-Za-z0-9_\-./]+$", RegexOptions.Compiled);

    /// <summary>
    /// Baut einen sicheren absoluten Pfad unterhalb eines Basisverzeichnisses auf.
    /// Regeln:
    /// - key: Backslashes werden zu Slashes normalisiert; nur A–Z a–z 0–9 _ - . / erlaubt.
    /// - key-Segmente dürfen weder "." noch ".." enthalten.
    /// - subFolder (optional): wird analog zum key validiert (Zeichen + Segmente).
    /// - Der resultierende Pfad muss innerhalb von <paramref name="baseRootFull"/> liegen.
    /// Ausnahmen:
    /// - ArgumentNullException: falls baseRootFull/tenantIdString/key leer oder null sind.
    /// - InvalidOperationException: bei ungültigen Zeichen/Segmenten oder Root-Escape.
    /// </summary>
    /// <param name="baseRootFull">Absoluter Basis-Pfad (wird normalisiert, Trailing-Separator erzwungen).</param>
    /// <param name="tenantIdString">Tenant-Identifikator (als Ordnername verwendet).</param>
    /// <param name="key">Ressourcenschlüssel (z. B. "folder/sub/file.json").</param>
    /// <param name="subFolder">Optionaler Unterordner (z. B. "static" oder "v1/assets").</param>
    /// <returns>Vollqualifizierter, sicherer Pfad innerhalb des Basisverzeichnisses.</returns>
    public static string BuildSafeFullPath(
        string baseRootFull,
        string tenantIdString,
        string key,
        string? subFolder = null)
    {
        if (string.IsNullOrWhiteSpace(baseRootFull)) throw new ArgumentNullException(nameof(baseRootFull));
        if (string.IsNullOrWhiteSpace(tenantIdString)) throw new ArgumentNullException(nameof(tenantIdString));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

        // Key validieren
        var normKey = key.Replace('\\', '/');
        if (!Allowed.IsMatch(normKey))
            throw new InvalidOperationException("Invalid characters in key.");

        // Explizit: kein führender/abschließender oder doppelter Separator im key
        if (normKey.StartsWith("/", StringComparison.Ordinal) || normKey.EndsWith("/", StringComparison.Ordinal) ||
            normKey.Contains("//", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid path segments.");

        var segments = normKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException("Invalid path segments.");

        // subFolder strikt validieren (analog zum key)
        if (!string.IsNullOrWhiteSpace(subFolder))
        {
            var sub = subFolder.Replace('\\', '/');
            if (!Allowed.IsMatch(sub))
                throw new InvalidOperationException("Invalid characters in subFolder.");

            // Explizit: kein führender oder abschließender Separator erlaubt
            if (sub.StartsWith("/", StringComparison.Ordinal) || sub.EndsWith("/", StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid subFolder segments.");

            // Doppel-Slash in der Mitte erkennen (würde sonst durch RemoveEmptyEntries verschwinden)
            if (sub.Contains("//", StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid subFolder segments.");

            var subSegs = sub.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (subSegs.Length == 0 || subSegs.Any(s => s is "." or ".."))
                throw new InvalidOperationException("Invalid subFolder segments.");
        }

        // Root mit garantiertem Trailing-Separator
        var rootNorm = EnsureTrailingSeparator(Path.GetFullPath(baseRootFull));

        // Pfad zusammenbauen
        var parts = new List<string> { rootNorm, tenantIdString };
        if (!string.IsNullOrWhiteSpace(subFolder)) parts.Add(subFolder);
        parts.AddRange(segments);

        var combined = Path.Combine(parts.ToArray());
        var full = Path.GetFullPath(combined);

        // Security: erzwingen, dass full im baseRootFull liegt
        if (!IsUnderRoot(full, rootNorm))
            throw new InvalidOperationException("Path escapes root.");

        return full;
    }

    /// <summary>
    /// Schreibt Bytes atomar an die durch baseRoot/tenantId/key[+subFolder] bestimmte Datei.
    /// Ablauf:
    /// - Validiert Eingaben, ermittelt sicheren Zielpfad via BuildSafeFullPath.
    /// - Stellt sicher, dass das Zielverzeichnis existiert.
    /// - Schreibt erst in eine temporäre Datei im Zielverzeichnis.
    /// - Berechnet SHA-256 (Hex) über die geschriebenen Bytes.
    /// - Ersetzt die Zieldatei atomar durch die temporäre (Move/Replace).
    /// Rückgabe: (fullPath, etag, fileInfo).
    /// </summary>
    /// <exception cref="ArgumentNullException">Wenn baseRoot/tenantId/key oder content ungültig sind.</exception>
    /// <exception cref="OperationCanceledException">Bei Abbruch.</exception>
    public static async Task<(string fullPath, string etag, FileInfo fileInfo)> WriteAtomicAsync(
        string baseRootFull,
        string tenantIdString,
        string key,
        byte[] content,
        string? subFolder = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseRootFull)) throw new ArgumentNullException(nameof(baseRootFull));
        if (string.IsNullOrWhiteSpace(tenantIdString)) throw new ArgumentNullException(nameof(tenantIdString));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
        if (content is null) throw new ArgumentNullException(nameof(content));

        ct.ThrowIfCancellationRequested();

        var targetPath = BuildSafeFullPath(baseRootFull, tenantIdString, key, subFolder);
        var dir = Path.GetDirectoryName(targetPath) ?? throw new InvalidOperationException("Invalid target directory.");

        Directory.CreateDirectory(dir);

        // Temporäre Datei im Zielverzeichnis
        var tempName = Path.Combine(dir, $".tmp_{Guid.NewGuid():N}.tmp");

        try
        {
            // Inhalt schreiben
            await File.WriteAllBytesAsync(tempName, content, ct).ConfigureAwait(false);

            // Hash berechnen (über content; identisch zu Dateiinhalt)
            var hash = SHA256.HashData(content);
            var etag = Convert.ToHexString(hash);

            // Zielstub sicherstellen, damit Replace immer möglich ist
            if (!File.Exists(targetPath))
            {
                try
                {
                    using var _ = new FileStream(targetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    // keine Inhalte schreiben; nur Datei anlegen falls fehlend
                }
                catch
                {
                    // Falls ein anderer Thread sie gerade erzeugt hat, ignorieren
                }
            }

            // Atomar ersetzen (ohne Backup). Bei parallelen Writes gewinnt der zuletzt durchgelaufene.
            File.Replace(tempName, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);

            var fi = new FileInfo(targetPath);
            return (targetPath, etag, fi);
        }
        catch
        {
            try { if (File.Exists(tempName)) File.Delete(tempName); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>
    /// Öffnet eine Datei nur-lesend (ShareRead) und liefert:
    /// - einen asynchronen Lese-Stream,
    /// - einen SHA-256-Hash als hexadezimales ETag,
    /// - die zugehörige FileInfo.
    /// Der Hash wird über ein separates Handle berechnet, bevor der Stream zurückgegeben wird.
    /// </summary>
    /// <param name="fullPath">Vollqualifizierter Pfad zu einer existierenden Datei.</param>
    /// <param name="ct">Abbruch-Token.</param>
    /// <exception cref="FileNotFoundException">Wenn die Datei nicht existiert.</exception>
    /// <exception cref="OperationCanceledException">Wenn der Aufruf abgebrochen wurde.</exception>
    public static async Task<(FileStream stream, string etag, FileInfo fileInfo)> OpenReadWithHashAsync(
        string fullPath,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", fullPath);

        var fi = new FileInfo(fullPath);

        // Hash berechnen (separates Handle, um den Rückgabe-Stream nicht zu beeinflussen)
        string etag;
        using (var sha = SHA256.Create())
        using (var fsHash = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
        {
            var hash = await sha.ComputeHashAsync(fsHash, ct).ConfigureAwait(false);
            etag = Convert.ToHexString(hash);
        }

        // Streaming-Handle (asynchron, SequentialScan-Hint)
        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan | FileOptions.Asynchronous);

        return (stream, etag, fi);
    }

    /// <summary>
    /// Sehr einfache Content-Type-Heuristik anhand der Dateiendung.
    /// Gibt null zurück, wenn unbekannt.
    /// </summary>
    public static string? GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json" => "application/json",
            ".yaml" or ".yml" => "application/yaml",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript",
            ".txt" => "text/plain; charset=utf-8",
            ".csv" => "text/csv; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".gz" => "application/gzip",
            ".tar" => "application/x-tar",
            ".rar" => "application/vnd.rar",
            ".7z" => "application/x-7z-compressed",
            ".pem" => "application/x-pem-file",
            ".crt" or ".cer" => "application/x-x509-ca-cert",
            ".pfx" or ".p12" => "application/x-pkcs12",
            _ => null
        };
    }

    // Prüft, ob fullPath unterhalb von rootWithSep liegt (rootWithSep: normalisiert + Trailing-Separator).
    private static bool IsUnderRoot(string fullPath, string rootWithSep)
    {
        var full = Path.GetFullPath(fullPath);
        return full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }

    // Stellt sicher, dass ein abschließender DirectorySeparator vorhanden ist.
    private static string EnsureTrailingSeparator(string rootFull)
    {
        if (string.IsNullOrEmpty(rootFull)) return rootFull;
        var sep = Path.DirectorySeparatorChar;
        var alt = Path.AltDirectorySeparatorChar;
        if (rootFull.EndsWith(sep) || rootFull.EndsWith(alt)) return rootFull;
        return rootFull + sep;
    }
}
