using System.Globalization;

namespace Skynet.Core.i18n;

/// <summary>
/// Ein einfacher, performanter Renderer für benannte Platzhalter.
/// Syntax: "Hallo {Name}, heute ist {Date:d}."
/// <para>
/// Features:
/// - Unterstützt .NET Standard Format Strings (z. B. :N2, :yyyy-MM-dd).
/// - Tolerant: Unbekannte Platzhalter bleiben im Text stehen (Debugging-Hilfe).
/// - Performance: Nutzt StringBuilder und vermeidet Regex für das Parsing.
/// </summary>
public sealed class SimpleTemplateRenderer : ITemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        if (parameters == null || parameters.Count == 0) return template;

        // Optimierung: Wenn keine Klammern da sind, nix tun.
        if (template.IndexOf('{') == -1) return template;

        var sb = new StringBuilder(template.Length); // Startgröße ca. wie Template
        var len = template.Length;
        var i = 0;

        while (i < len)
        {
            var c = template[i];

            if (c == '{')
            {
                // Check auf Escaping "{{"
                if (i + 1 < len && template[i + 1] == '{')
                {
                    sb.Append('{');
                    i += 2;
                    continue;
                }

                // Platzhalter Anfang gefunden, suche Ende '}'
                var closingBraceIndex = template.IndexOf('}', i);
                if (closingBraceIndex == -1)
                {
                    // Kein schließendes Element -> Rest als Text behandeln
                    sb.Append(template.Substring(i));
                    break;
                }

                // Inhalt extrahieren: "Name" oder "Date:yyyy"
                var rawToken = template.Substring(i + 1, closingBraceIndex - i - 1);
                
                // Parsing Key vs Format
                var formatSeparatorIndex = rawToken.IndexOf(':');
                string key;
                string? formatString = null;

                if (formatSeparatorIndex >= 0)
                {
                    key = rawToken.Substring(0, formatSeparatorIndex).Trim();
                    formatString = rawToken.Substring(formatSeparatorIndex + 1);
                }
                else
                {
                    key = rawToken.Trim();
                }

                // Lookup
                if (parameters.TryGetValue(key, out var value))
                {
                    sb.Append(FormatValue(value, formatString, culture));
                }
                else
                {
                    // Key nicht gefunden -> Token unverändert lassen (User sieht "{MissingKey}")
                    sb.Append('{').Append(rawToken).Append('}');
                }

                i = closingBraceIndex + 1;
            }
            else if (c == '}')
            {
                // Check auf Escaping "}}"
                if (i + 1 < len && template[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                    continue;
                }
                
                // Einzelne '}' ohne '{' davor ist valid text
                sb.Append('}');
                i++;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    private static string FormatValue(object? value, string? format, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        if (value is IFormattable formattable)
        {
            return formattable.ToString(format, culture);
        }

        return value.ToString() ?? string.Empty;
    }
}
