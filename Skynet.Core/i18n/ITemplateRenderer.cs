using System.Collections.Generic;
using System.Globalization;

namespace Skynet.Core.i18n;

/// <summary>
/// Rendert einen Template-String mit Parametern in den finalen Text.
/// <para>
/// Die Implementierung bestimmt die Syntax (z. B. "{Name}" oder "{{Name}}").
/// Muss threadsicher sein.
/// </para>
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Ersetzt Platzhalter im Template durch die Werte aus den Parametern.
    /// </summary>
    /// <param name="template">Der Rohtext mit Platzhaltern (z. B. "Hello {Name}").</param>
    /// <param name="parameters">Die Werte für die Platzhalter.</param>
    /// <param name="culture">Die Zielkultur für die Formatierung von Zahlen/Daten.</param>
    /// <returns>Der final formatierte String.</returns>
    string Render(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture);
}
