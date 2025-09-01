// Skynet.Tests/Localization/CultureThreadScopeTests.cs
using System.Globalization;
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization
{
    public class CultureThreadScopeTests
    {
        [Fact]
        public void Scope_Applies_And_Restores_Culture()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;

            var store = new InMemoryLocalizationStore();
            store.SetCulture("de-DE");
            var provider = new CurrentCultureProvider(store, new LocalizationOptions());

            using (new CultureThreadScope(provider))
            {
                Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
                Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name);
            }

            // Restored
            Assert.Equal(originalCulture.Name, CultureInfo.CurrentCulture.Name);
            Assert.Equal(originalUICulture.Name, CultureInfo.CurrentUICulture.Name);
        }

        [Fact]
        public void Nested_Scopes_Restore_Properly()
        {
            var store = new InMemoryLocalizationStore();
            var options = new LocalizationOptions { DefaultCulture = "en-US" };

            // outer -> de-DE
            store.SetCulture("de-DE");
            var providerOuter = new CurrentCultureProvider(store, options);

            using (new CultureThreadScope(providerOuter))
            {
                Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);

                // inner -> en-GB
                store.SetCulture("en-GB");
                var providerInner = new CurrentCultureProvider(store, options);

                using (new CultureThreadScope(providerInner))
                {
                    Assert.Equal("en-GB", CultureInfo.CurrentCulture.Name);
                }

                // back to outer
                Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
            }
        }
    }
}
