using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Localization
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers localization primitives. You can bind LocalizationOptions via Options pattern later.
        /// </summary>
        public static IServiceCollection AddLocalizationCore(
            this IServiceCollection services,
            LocalizationOptions? options = null)
        {
            options ??= new LocalizationOptions();

            // Options instance (simple for now; can switch to IOptions later)
            services.AddSingleton(options);

            services.AddSingleton<ILocalizationStore>(sp =>
            {
                var localizationOptions = sp.GetRequiredService<LocalizationOptions>();
                var inner = new InMemoryLocalizationStore();
                return new ValidatedLocalizationStore(inner, localizationOptions);
            });
            
            services.AddScoped<ICurrentCultureProvider, CurrentCultureProvider>();
            services.AddScoped<IDateTimeFormatter, DateTimeFormatter>();
            services.AddScoped<ICultureThreadScopeFactory, CultureThreadScopeFactory>();


            return services;
        }
    }
}