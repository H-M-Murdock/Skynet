using System;
using System.Globalization;

namespace Skynet.Core.Localization
{
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
            return value.ToString(patternAliasOrCustom, culture);
        }
    }
}