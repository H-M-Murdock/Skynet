using System;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Provides functionality to format dates and times according to specific patterns or culture-aware aliases.
    /// Decouples formatting logic from the system time source.
    /// </summary>
    public interface IDateTimeFormatter
    {
        /// <summary>
        /// Formats a specific date and time using a pattern alias (e.g. "ShortDate") or a custom format string.
        /// To format the current time from an <see cref="Skynet.Core.Time.IClock"/>, pass <c>clock.UtcNow</c>.
        /// </summary>
        /// <param name="value">The date and time value to format.</param>
        /// <param name="patternAliasOrCustom">A predefined alias key (resolved via configuration) or a standard .NET format string (e.g. "yyyy-MM-dd").</param>
        /// <returns>The formatted date string.</returns>
        string Format(DateTime value, string patternAliasOrCustom);

        /// <summary>
        /// Formats a specific date and time using a pattern alias or custom format string.
        /// Supports DateTimeOffset for timezone-aware formatting.
        /// </summary>
        /// <param name="value">The date and time with offset to format.</param>
        /// <param name="patternAliasOrCustom">A predefined alias key or standard .NET format string.</param>
        /// <returns>The formatted date string.</returns>
        string Format(DateTimeOffset value, string patternAliasOrCustom);
    }
}