// Skynet.Core/Localization/ApplicationBuilderExtensions.cs
using Microsoft.AspNetCore.Builder;

namespace Skynet.Core.Localization
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseSkynetCulture(this IApplicationBuilder app)
            => app.UseMiddleware<CultureMiddleware>();
    }
}