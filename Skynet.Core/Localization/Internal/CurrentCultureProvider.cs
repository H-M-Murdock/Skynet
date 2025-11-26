// Skynet.Core/Localization/CurrentCultureProvider.cs
using System;
using System.Globalization;
using System.Linq;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Resolves the current culture from the configured <see cref="ILocalizationStore"/>.
    /// Applies fallback logic to default culture if the stored value is missing, invalid, or not supported.
    /// </summary>
    public class CurrentCultureProvider : ICurrentCultureProvider
    {
        private readonly ILocalizationStore _store;
        private readonly LocalizationOptions _options;
        private readonly CultureInfo _defaultCulture;

        public CurrentCultureProvider(ILocalizationStore store, LocalizationOptions options)
        {
            _store = store;
            _options = options;
            
            // Pre-resolve default culture to fail fast on configuration errors
            try
            {
                _defaultCulture = CultureInfo.GetCultureInfo(_options.DefaultCulture);
            }
            catch (CultureNotFoundException)
            {
                // Fallback if config is broken: System Invariant or throw? 
                // Usually better to throw during startup than runtime.
                throw new InvalidOperationException($"Configured DefaultCulture '{_options.DefaultCulture}' is invalid.");
            }
        }

        public CultureInfo GetCulture()
        {
            var name = _store.GetCultureName();
            
            if (string.IsNullOrWhiteSpace(name))
                return _defaultCulture;

            // 1. Check Whitelist (if configured)
            if (_options.SupportedCultures is { Count: > 0 } && 
                !IsSupported(name))
            {
                return _defaultCulture;
            }

            // 2. Check System Validity (fast check)
            if (!CultureCatalog.Known.Contains(name))
                return _defaultCulture;

            // 3. Resolve
            try
            {
                return CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                return _defaultCulture;
            }
        }

        private bool IsSupported(string name)
        {
            // Note: For small lists (e.g. < 20), LINQ Any is fast enough.
            return _options.SupportedCultures!.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}