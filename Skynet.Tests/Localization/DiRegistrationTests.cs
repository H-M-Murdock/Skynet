// Skynet.Tests/Localization/DiRegistrationTests.cs
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization
{
    public class DiRegistrationTests
    {
        [Fact]
        public void AddSkynetLocalizationCore_Resolves_All_Services()
        {
            var services = new ServiceCollection();
            services.AddLocalizationCore(new LocalizationOptions { DefaultCulture = "en-US" });

            // validateScopes: true -> verhindert Resolve von Scoped aus Root
            using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            // Singleton darf aus Root geholt werden
            Assert.NotNull(sp.GetRequiredService<ILocalizationStore>());

            // Scoped Services IMMER aus einem Scope auflösen
            using var scope = sp.CreateScope();
            var scoped = scope.ServiceProvider;

            Assert.NotNull(scoped.GetRequiredService<ICurrentCultureProvider>());
            Assert.NotNull(scoped.GetRequiredService<IDateTimeFormatter>());
        }
        
        [Fact]
        public void ResolvingScopedFromRoot_Throws_WhenValidateScopesEnabled()
        {
            var services = new ServiceCollection();
            services.AddLocalizationCore();
            using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<ICurrentCultureProvider>());
        }

        
    }
    
    
}