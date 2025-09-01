// Skynet.Core/Localization/ValidatedLocalizationStore.cs
using System;
using System.Globalization;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Wraps another ILocalizationStore and validates culture names before setting.
    /// </summary>
    public class ValidatedLocalizationStore : ILocalizationStore
    {
        private readonly ILocalizationStore _inner;
        private readonly LocalizationOptions _options;

        public ValidatedLocalizationStore(ILocalizationStore inner, LocalizationOptions options)
        {
            _inner = inner;
            _options = options;
        }

        public string? GetCultureName() => _inner.GetCultureName();

        public void SetCulture(string cultureName) => _inner.SetCulture(cultureName);

        public bool TrySetCulture(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return false;

            // Check whitelist (if defined)
            if (_options.SupportedCultures is { Count: > 0 } &&
                !_options.SupportedCultures.Contains(cultureName, StringComparer.OrdinalIgnoreCase))
                return false;

            // Check if .NET knows the culture
            try
            {
                var ci = CultureInfo.GetCultureInfo(cultureName);
                _inner.SetCulture(ci.Name); // normalized
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}