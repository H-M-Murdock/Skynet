using System.Globalization;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Provides access to the current effective culture for formatting and localization.
    /// Abstraction over <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/> to allow easy mocking and substitution.
    /// </summary>
    public interface ICurrentCultureProvider
    {
        /// <summary>
        /// Gets the culture used for formatting numbers, dates, and currency (equivalent to <see cref="CultureInfo.CurrentCulture"/>).
        /// </summary>
        /// <returns>The active culture info.</returns>
        CultureInfo GetCulture();

        // Optional: Wenn du Texte übersetzen willst, brauchst du oft das hier getrennt.
        // Wenn du es simpel hältst, nutzen viele Apps einfach dieselbe Culture für beides.
        // Ich lasse es erst mal bei GetCulture() wie im Original, aber dokumentiere es sauber.
    }
}