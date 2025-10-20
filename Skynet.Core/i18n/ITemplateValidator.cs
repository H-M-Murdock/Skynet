namespace Skynet.Core.i18n;

/// <summary>
/// Prüft Templates auf syntaktische Korrektheit und Placeholder-Konsistenz.
/// Typische Prüfungen:
/// - Platzhalter-Extraktion (z. B. {OrderId}) und Validierung von Namen/Kollisionsregeln.
/// - Konsistenz über Sprachvarianten eines Keys (z. B. de vs. en haben denselben Placeholder-Satz).
/// - Optional: verbotene Konstrukte (z. B. verschachtelte Klammern je Syntax).
/// </summary>
public interface ITemplateValidator
{
    /// <summary>
    /// Validiert ein einzelnes Template (Syntax, Placeholder-Regeln).
    /// </summary>
    TemplateValidationResult Validate(string template);

    /// <summary>
    /// Prüft die Konsistenz mehrerer Varianten desselben Keys (z. B. de, en, fr).
    /// Erwartung: Alle Varianten verwenden denselben Satz an Platzhaltern.
    /// Abweichungen werden als Fehler oder Warnungen zurückgegeben.
    /// </summary>
    TemplateSetValidationResult ValidateConsistency(IReadOnlyDictionary<string /*culture*/, string /*template*/> variants);
}