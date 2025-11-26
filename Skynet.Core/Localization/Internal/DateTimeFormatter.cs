
using System;
using System.Globalization;
// IClock is no longer needed here directly, but DateTime types are standard.

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Standard implementation of IDateTimeFormatter.
    /// Uses ICurrentCultureProvider to determine the target culture for formatting.
    /// </summary>
    public class DateTimeFormatter : IDateTimeFormatter
    {
        private readonly ICurrentCultureProvider _cultureProvider;

        public DateTimeFormatter(ICurrentCultureProvider cultureProvider)
        {
            _cultureProvider = cultureProvider;
        }

        public string Format(DateTime value, string patternAliasOrCustom)
        {
            var culture = _cultureProvider.GetCulture();
            var format = ResolvePattern(patternAliasOrCustom, culture);
            return value.ToString(format, culture);
        }

        public string Format(DateTimeOffset value, string patternAliasOrCustom)
        {
            var culture = _cultureProvider.GetCulture();
            var format = ResolvePattern(patternAliasOrCustom, culture);
            return value.ToString(format, culture);
        }

        /// <summary>
        /// Resolves aliases like "ShortDate" to standard .NET format strings.
        /// If no alias matches, returns the input string as a custom format.
        /// </summary>
        private static string ResolvePattern(string alias, CultureInfo culture)
        {
            // Simple hardcoded alias resolution. 
            // Could be moved to LocalizationOptions later for flexibility.
            return alias switch
            {
                "ShortDate" => "d",
                "LongDate" => "D",
                "ShortTime" => "t",
                "LongTime" => "T",
                "Full" => "F",
                "Iso" => "O", // Round-trip date/time pattern
                _ => alias
            };
        }
    }
}