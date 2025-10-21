using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

/// <summary>
/// Pfad-Template-Renderer:
/// - Ersetzt bekannte Tokens in geschweiften Klammern case-insensitive.
/// - Unterstützt DateTime-Format-Tokens direkt (z. B. {yyyy-MM-dd}, {HHmmss}).
/// - Nutzt State ("TenantId","Component") als Quelle für tenant/component.
/// - Säubert Segmente von unzulässigen Zeichen und entfernt leere Segmente.
/// - Reduziert doppelte Slashes, entfernt führende/trailing Slashes.
/// </summary>
public sealed class DefaultPathTemplateRenderer : IPathTemplateRenderer
{
    // Ungültige Zeichen: OS-nahe Auswahl (breit gefasst); Path.GetInvalidPathChars enthält auch Steuerzeichen.
    private static readonly char[] InvalidChars = new[] { '"', '<', '>', '|', '\0', (char)1, (char)2, (char)3, ':', '*', '?', '\\' };
    private static readonly Regex TokenRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    public string Render(ILogEvent evt, DateTimeOffset now)
        => Render(evt, now, "{tenant}/{component}/{yyyy-MM-dd}.log");

    public string Render(ILogEvent evt, DateTimeOffset now, string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;

        var result = TokenRegex.Replace(template, m =>
        {
            var raw = m.Groups[1].Value;
            var token = raw.Trim();

            // Datums-/Zeit-Format
            if (IsDateTimeFormat(token))
            {
                return Sanitize(now.ToString(token));
            }

            // Standardisierte Tokens (case-insensitive)
            switch (token.ToLowerInvariant())
            {
                case "tenant":
                    return Sanitize(FindStateValue(evt.State, "TenantId"));
                case "component":
                    return Sanitize(FindStateValue(evt.State, "Component"));
                case "category":
                    return Sanitize(evt.CategoryName);
                case "operation":
                    return Sanitize(evt.Operation);
                case "level":
                    return Sanitize(evt.LevelString);
                case "eventid":
                    return evt.EventId.Id.ToString();
                case "globalid":
                    return Sanitize(evt.GlobalEventId);
                case "corr":
                    return Sanitize(evt.CorrelationId);
                case "trace":
                    return Sanitize(evt.TraceId);
                case "span":
                    return Sanitize(evt.SpanId);
                case "utc":
                    return Sanitize(now.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ss'Z'"));
                case "local":
                    return Sanitize(now.ToLocalTime().ToString("yyyy-MM-ddTHH-mm-sszzz"));
                default:
                    // Unbekannt -> leer (wird später wegsaniert)
                    return string.Empty;
            }
        });

        // Segmente splitten, leere entfernen, erneut zusammensetzen
        var parts = result.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder(result.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            var seg = parts[i].Trim();
            if (seg.Length == 0) continue;

            // Nachsanitizing: entferne verbleibende ungültige Zeichen
            seg = Sanitize(seg);
            if (seg.Length == 0) continue;

            if (sb.Length > 0) sb.Append('/');
            sb.Append(seg);
        }

        return sb.ToString();
    }

    private static bool IsDateTimeFormat(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        // Erlaube nur Zeichen, die in DateTime-Formatstrings üblich sind
        // Format-Buchstaben: y M d H h m s f F z K t
        // Separatoren: - _ : / . ' Z (Z nur als Literal gedacht, aber wir lassen es durch)
        foreach (var ch in token)
        {
            if (ch is 'y' or 'M' or 'd' or 'H' or 'h' or 'm' or 's' or 'f' or 'F' or 'z' or 'K' or 't'
                or '-' or '_' or ':' or '/' or '.' or '\'' or 'Z')
            {
                continue;
            }
            return false;
        }

        // Muss mindestens einen Format-Buchstaben enthalten
        if (!(token.Contains('y') || token.Contains('M') || token.Contains('d') ||
              token.Contains('H') || token.Contains('h') || token.Contains('m') ||
              token.Contains('s') || token.Contains('f') || token.Contains('F') ||
              token.Contains('z') || token.Contains('K') || token.Contains('t')))
        {
            return false;
        }

        return true;
    }

    private static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var s = input.AsSpan();
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (Array.IndexOf(InvalidChars, ch) >= 0) continue;
            // Slash wird als Segmenttrenner verstanden → vermeiden innerhalb eines Segments
            if (ch == '/') continue;
            sb.Append(ch);
        }
        // Trim Punkte/Spaces an Segmentenden
        var res = sb.ToString().Trim().Trim('.');
        // Doppelte Punkte nicht entfernen (z. B. "Cat.Sub" erlaubt)
        return res;
    }

    private static string FindStateValue(IReadOnlyList<KeyValuePair<string, object?>> state, string key)
    {
        if (state is null || state.Count == 0) return string.Empty;
        for (int i = 0; i < state.Count; i++)
        {
            var kv = state[i];
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value?.ToString() ?? string.Empty;
        }
        return string.Empty;
    }
}
