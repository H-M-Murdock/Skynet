// Skynet.Tests/Localization/LocalizationStoreTests.cs
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization
{
    public class LocalizationStoreTests
    {
        [Fact]
        public void TrySetCulture_Accepts_ValidCulture()
        {
            var store = new ValidatedLocalizationStore(new InMemoryLocalizationStore(),
                new LocalizationOptions { SupportedCultures = new[] { "en-US", "de-DE" } });

            Assert.True(store.TrySetCulture("de-DE"));
            Assert.Equal("de-DE", store.GetCultureName());
        }

        [Fact]
        public void TrySetCulture_Rejects_When_NotInWhitelist()
        {
            var store = new ValidatedLocalizationStore(
                new InMemoryLocalizationStore(),
                new LocalizationOptions { SupportedCultures = new[] { "en-US", "de-DE" } }
            );

            Assert.False(store.TrySetCulture("fr-FR"));
            Assert.Null(store.GetCultureName());
        }

        [Fact]
        public void TrySetCulture_Respects_Whitelist()
        {
            var store = new ValidatedLocalizationStore(new InMemoryLocalizationStore(),
                new LocalizationOptions { SupportedCultures = new[] { "en-US" } });

            Assert.False(store.TrySetCulture("de-DE"));
            Assert.True(store.TrySetCulture("en-US"));
        }
    }
}