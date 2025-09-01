using System;
using System.Globalization;
using Skynet.Core.Time;

namespace Skynet.Core.Localization
{
    public class DateTimeFormatter : IDateTimeFormatter
    {
        private readonly ICurrentCultureProvider _cultureProvider;

        public DateTimeFormatter(ICurrentCultureProvider cultureProvider)
        {
            _cultureProvider = cultureProvider;
        }

        public string Format(IClock clock, string patternAliasOrCustom)
        {
            var culture = _cultureProvider.GetCulture();
            var now = clock.UtcNow; 
            return now.ToString(patternAliasOrCustom, culture);
        }
    }
}