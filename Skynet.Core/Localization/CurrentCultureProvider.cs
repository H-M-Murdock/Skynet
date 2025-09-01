// Skynet.Core/Localization/CurrentCultureProvider.cs
using System;
using System.Globalization;
using System.Linq;

namespace Skynet.Core.Localization
{
    public class CurrentCultureProvider : ICurrentCultureProvider
    {
        private readonly ILocalizationStore _store;
        private readonly LocalizationOptions _options;

        public CurrentCultureProvider(ILocalizationStore store, LocalizationOptions options)
        {
            _store = store;
            _options = options;
        }

        public CultureInfo GetCulture()
        {
            var name = _store.GetCultureName();
            if (string.IsNullOrWhiteSpace(name))
                return CultureInfo.GetCultureInfo(_options.DefaultCulture);

            // Whitelist (falls gesetzt)
            if (_options.SupportedCultures is { Count: > 0 } && 
                !_options.SupportedCultures.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
            {
                return CultureInfo.GetCultureInfo(_options.DefaultCulture);
            }

            // Ist es eine bekannte Culture?
            var isKnown = CultureCatalog.Known.Contains(name);
            if (!isKnown)
                return CultureInfo.GetCultureInfo(_options.DefaultCulture);

            try
            {
                // GetCultureInfo normalisiert und cached
                return CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo(_options.DefaultCulture);
            }
        }
    }
}