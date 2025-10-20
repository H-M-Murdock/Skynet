// Skynet.Core/ResourceProvider/Internal/FileResourceUtil.cs

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Skynet.Core;

internal static class IoUtilities
{
    // A–Z a–z 0–9 _ - . /  (Slash für Segmente, Backslashes werden vorher normalisiert)
    // Keine Leerzeichen: bewusst strikt für serverseitige Ressourcenkeys.
    private static readonly Regex Allowed = new(@"^[A-Za-z0-9_\-./]+$", RegexOptions.Compiled);

    /// <summary>
    /// Normalisiert den key (Backslash->Slash), prüft Zeichen/Segmente und
    /// liefert den sicheren FullPath innerhalb von baseRoot[/subFolder]/tenantId[/segments].
    /// Wirft InvalidOperationException bei Verletzungen (Aufrufer wandelt ggf. in NotFound-Reason).
    /// </summary>
    public static string BuildSafeFullPath(
        string baseRootFull,
        string tenantIdString,
        string key,
        string? subFolder = null)
    {
        if (string.IsNullOrWhiteSpace(baseRootFull)) throw new ArgumentNullException(nameof(baseRootFull));
        if (string.IsNullOrWhiteSpace(tenantIdString)) throw new ArgumentNullException(nameof(tenantIdString));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

        var normKey = key.Replace('\\', '/');

        if (!Allowed.IsMatch(normKey))
            throw new InvalidOperationException("Invalid characters in key.");

        var segments = normKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException("Invalid path segments.");

        // Root mit garantiertem Trailing-Separator
        var rootNorm = EnsureTrailingSeparator(Path.GetFullPath(baseRootFull));

        var parts = new List<string> { rootNorm, tenantIdString };
        if (!string.IsNullOrWhiteSpace(subFolder)) parts.Add(subFolder);
        parts.AddRange(segments);

        var combined = Path.Combine(parts.ToArray());
        var full = Path.GetFullPath(combined);

        // Security: erzwingen, dass full im baseRootFull liegt (mit Trailing-Separator-Vergleich)
        if (!IsUnderRoot(full, rootNorm))
            throw new InvalidOperationException("Path escapes root.");

        return full;
    }

    /// <summary>
    /// Öffnet eine Datei ReadOnly/ShareRead und berechnet im separaten Handle SHA-256 als Hex-ETag.
    /// Gibt (stream, etag, fileInfo) zurück. Aufrufer ist für Dispose(stream) verantwortlich.
    /// </summary>
    public static async Task<(FileStream stream, string etag, FileInfo fileInfo)> OpenReadWithHashAsync(
        string fullPath,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", fullPath);

        var fi = new FileInfo(fullPath);

        string etag;
        using (var sha = SHA256.Create())
        using (var fsHash = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
        {
            var hash = await sha.ComputeHashAsync(fsHash, ct).ConfigureAwait(false);
            etag = Convert.ToHexString(hash);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous);
        return (stream, etag, fi);
    }

    /// <summary>
    /// Simple ContentType-Heuristik per Endung (optional). Kann später durch Resolver ersetzt werden.
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

    private static bool IsUnderRoot(string fullPath, string rootWithSep)
    {
        var full = Path.GetFullPath(fullPath);
        // rootWithSep ist bereits normalisiert und hat einen abschließenden Separator
        return full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string rootFull)
    {
        if (string.IsNullOrEmpty(rootFull)) return rootFull;
        var sep = Path.DirectorySeparatorChar;
        var alt = Path.AltDirectorySeparatorChar;
        if (rootFull.EndsWith(sep) || rootFull.EndsWith(alt)) return rootFull;
        return rootFull + sep;
    }
}
