using System.Threading.Tasks;
using Skynet.Core.Localization;
using Xunit;

namespace Skynet.Tests.Localization;

public class InMemoryLocalizationStoreTests
{
    [Fact]
    public async Task Store_ShouldIsolateValues_BetweenAsyncFlows()
    {
        // Arrange
        var store = new InMemoryLocalizationStore();

        // Act & Assert
        // Task 1 setzt "en-US"
        var task1 = Task.Run(async () =>
        {
            store.SetCulture("en-US");
            await Task.Delay(50); // Simuliere Arbeit
            Assert.Equal("en-US", store.GetCultureName());
        });

        // Task 2 setzt "de-DE" parallel
        var task2 = Task.Run(async () =>
        {
            store.SetCulture("de-DE");
            await Task.Delay(50); // Simuliere Arbeit
            Assert.Equal("de-DE", store.GetCultureName());
        });

        await Task.WhenAll(task1, task2);
    }

    [Fact]
    public void GetCultureName_ShouldReturnNull_Initially()
    {
        var store = new InMemoryLocalizationStore();
        Assert.Null(store.GetCultureName());
    }

    [Fact]
    public void SetCulture_ShouldOverwritePreviousValue()
    {
        var store = new InMemoryLocalizationStore();
        store.SetCulture("en-US");
        store.SetCulture("fr-FR");
        Assert.Equal("fr-FR", store.GetCultureName());
    }
}
