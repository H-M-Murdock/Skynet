// Skynet.Core/ResourceProvider/Internal/FileResourceUtil.cs

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Skynet.Core;

internal static class IoUtilities
{
    // A–Z a–z 0–9 _ - . /  (Slash für Segmente, Backslashes werden vorher normalisiert)
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

        var parts = new List<string> { baseRootFull, tenantIdString };
        if (!string.IsNullOrWhiteSpace(subFolder)) parts.Add(subFolder);
        parts.AddRange(segments);

        var combined = Path.Combine(parts.ToArray());
        var full = Path.GetFullPath(combined);

        // Security: erzwingen, dass full im baseRootFull liegt
        if (!full.StartsWith(baseRootFull, StringComparison.OrdinalIgnoreCase))
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
        using (var fsHash = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var hash = await sha.ComputeHashAsync(fsHash, ct).ConfigureAwait(false);
            etag = Convert.ToHexString(hash);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
            ".png"  => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg"  => "image/svg+xml",
            ".txt"  => "text/plain; charset=utf-8",
            _       => null
        };
    }
}
