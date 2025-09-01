// Skynet.Tests/Localization/CultureScopeDiTests.cs
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization
{
    public class CultureScopeDiTests
    {
        [Fact]
        public void Factory_Creates_Scope_That_Applies_Culture()
        {
            var services = new ServiceCollection();
            services.AddLocalizationCore(new LocalizationOptions { DefaultCulture = "en-US" });

            using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using var scope = sp.CreateScope();

            // set desired culture for this "request"
            var store = scope.ServiceProvider.GetRequiredService<ILocalizationStore>();
            store.SetCulture("fr-FR");

            var factory = scope.ServiceProvider.GetRequiredService<ICultureThreadScopeFactory>();

            using (factory.BeginScope())
            {
                Assert.Equal("fr-FR", System.Globalization.CultureInfo.CurrentCulture.Name);
            }
        }
    }
}