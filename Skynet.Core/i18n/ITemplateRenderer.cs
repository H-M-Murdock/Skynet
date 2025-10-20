using System.Collections.Generic;

namespace Skynet.Core.i18n;

/// <summary>
/// Rendert einen Template-Text mit Parametern zu einem finalen String.
/// Beispiele für Syntaxen: Serilog-ähnlich ({UserId}), ICU MessageFormat, Handlebars.
/// Anforderungen:
/// - Deterministisch, threadsicher.
/// - Fehlende Parameter sollen tolerant behandelt werden (z. B. Platzhalter stehen lassen).
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Rendert ein Template.
    /// template: Rohtext mit Platzhaltern (nie null; "" erlaubt).
    /// parameters: Schlüssel/Werte; null = keine Parameter.
    /// </summary>
    string Render(string template, IReadOnlyDictionary<string, object?>? parameters);
}
