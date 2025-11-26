using System.Collections.Generic;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Configuration options for the localization system.
    /// Typically registered in the DI container via the Options pattern.
    /// </summary>
    public class LocalizationOptions
    {
        /// <summary>
        /// Gets or sets the default culture to use for formatting (dates, numbers)
        /// when no specific culture is requested. Default is "en-US".
        /// </summary>
        public string DefaultCulture { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets the default UI culture to use for resource lookup (translations)
        /// when no specific culture is requested. Default is "en-US".
        /// </summary>
        public string DefaultUICulture { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets the list of supported culture codes (e.g. "en-US", "de-DE").
        /// If null, validation against a whitelist is disabled (all system cultures allowed).
        /// If set, only cultures in this list are accepted by <see cref="ILocalizationStore.TrySetCulture"/>.
        /// </summary>
        public IReadOnlyCollection<string>? SupportedCultures { get; set; }
    }
}