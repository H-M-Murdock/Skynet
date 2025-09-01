using System;
using System.Globalization;
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization
{
    [Collection("InvariantCultureCollection")]
    public class LocalizationTests
    {
        [Fact]
        public void DefaultCulture_Is_enUS()
        {
            var store = new InMemoryLocalizationStore(); // no set -> default
            var provider = new CurrentCultureProvider(store, new LocalizationOptions());
            Assert.Equal("en-US", provider.GetCulture().Name);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("en-GB")]
        public void SettingCulture_ReturnsSameValidCulture(string culture)
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture(culture);

            var provider = new CurrentCultureProvider(store, new LocalizationOptions());
            Assert.Equal(culture, provider.GetCulture().Name);
        }

        [Fact]
        public void InvalidCulture_FallsBackToDefault()
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture("xx-YY"); // invalid
            var options = new LocalizationOptions { DefaultCulture = "de-DE" };

            var provider = new CurrentCultureProvider(store, options);
            Assert.Equal("de-DE", provider.GetCulture().Name);
        }

        [Fact]
        public void DateTimeFormatter_Formats_ShortDate_ByCulture()
        {
            var store = new InMemoryLocalizationStore();
            var options = new LocalizationOptions { DefaultCulture = "en-US" };
            var provider = new CurrentCultureProvider(store, options);
            var formatter = new DateTimeFormatter(provider);

            var dt = new DateTime(2025, 7, 4, 13, 5, 0); // 4th of July
            // en-US short date is typically 7/4/2025
            Assert.Equal(dt.ToString("d", new CultureInfo("en-US")), formatter.Format(dt, DateTimePattern.ShortDate));

            store.SetCulture("de-DE");
            // de-DE short date is typically 04.07.2025
            Assert.Equal(dt.ToString("d", new CultureInfo("de-DE")), formatter.Format(dt, DateTimePattern.ShortDate));
        }

        [Fact]
        public void DateTimeFormatter_Formats_LongDateTime_ByCulture()
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture("de-DE");
            var options = new LocalizationOptions { DefaultCulture = "en-US" };
            var provider = new CurrentCultureProvider(store, options);
            var formatter = new DateTimeFormatter(provider);

            var dt = new DateTime(2025, 12, 31, 23, 59, 30);
            Assert.Equal(dt.ToString("F", new CultureInfo("de-DE")), formatter.Format(dt, DateTimePattern.LongDateTime));
        }

        [Fact]
        public void DateTimeFormatter_CustomPattern_IsApplied()
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture("en-GB");
            var provider = new CurrentCultureProvider(store, new LocalizationOptions());
            var formatter = new DateTimeFormatter(provider);

            var dt = new DateTime(2025, 1, 2, 3, 4, 5);
            var custom = "yyyy-MM-dd HH:mm";
            Assert.Equal(dt.ToString(custom, new CultureInfo("en-GB")), formatter.Format(dt, custom));
        }
        
        /// <summary>
        /// Nachweisen, dass mehrere „Requests“ sich nicht überschreiben.
        /// </summary>
        [Fact]
        public void InMemoryLocalizationStore_Isolates_PerAsyncFlow()
        {
            var store = new InMemoryLocalizationStore();
            var options = new LocalizationOptions { DefaultCulture = "en-US" };

            var cultures = new[] { "de-DE", "en-GB", "fr-FR", "ja-JP" };
            Parallel.ForEach(cultures, c =>
            {
                store.SetCulture(c);
                var provider = new CurrentCultureProvider(store, options);
                Assert.Equal(c, provider.GetCulture().Name);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("xx-YY")]   // invalid
        public void InvalidOrEmptyCulture_FallsBackToDefault(string? input)
        {
            var store = new InMemoryLocalizationStore();
            if (input != null) store.SetCulture(input);
            var options = new LocalizationOptions { DefaultCulture = "de-DE" };
            var provider = new CurrentCultureProvider(store, options);

            Assert.Equal("de-DE", provider.GetCulture().Name);
        }

        [Theory]
        [InlineData("de-de", "de-DE")]
        [InlineData("EN-gb", "en-GB")]
        public void CultureName_IsCaseInsensitive_AndNormalizes(string input, string expected)
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture(input);
            var provider = new CurrentCultureProvider(store, new LocalizationOptions());
            Assert.Equal(expected, provider.GetCulture().Name);
        }

        [Theory]
        [InlineData("en-US", DateTimePattern.ShortDate)]
        [InlineData("de-DE", DateTimePattern.ShortDate)]
        [InlineData("en-GB", DateTimePattern.LongDateTime)]
        [InlineData("fr-FR", "yyyy-MM-dd HH:mm")]
        public void DateTimeFormatter_Respects_Culture_And_Pattern(string culture, string pattern)
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture(culture);
            var provider = new CurrentCultureProvider(store, new LocalizationOptions());
            var formatter = new DateTimeFormatter(provider);

            var dt = new DateTime(2025, 07, 04, 13, 05, 00);
            var expected = dt.ToString(pattern, new CultureInfo(culture));
            Assert.Equal(expected, formatter.Format(dt, pattern));
        }

        
        // Whitelist respektiert
        [Theory]
        [InlineData("fr-FR", new[] { "en-US", "de-DE" }, "en-US")]
        [InlineData("de-DE", new[] { "en-US", "de-DE" }, "de-DE")]
        public void SupportedCultures_Whitelist_Is_Enforced(string requested, string[] allowed, string expected)
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture(requested);
            var options = new LocalizationOptions { DefaultCulture = "en-US", SupportedCultures = allowed };
            var provider = new CurrentCultureProvider(store, options);

            Assert.Equal(expected, provider.GetCulture().Name);
        }

        // Normalisierung (kanonischer Name)
        [Theory]
        [InlineData("EN-gb", "en-GB")]
        public void CultureName_Is_Normalized(string input, string normalized)
        {
            var store = new InMemoryLocalizationStore();
            store.SetCulture(input);
            var provider = new CurrentCultureProvider(store, new LocalizationOptions());
            Assert.Equal(normalized, provider.GetCulture().Name);
        }

        
    }
}
