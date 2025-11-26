using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Middleware that applies the current culture to the request's execution context.
    /// It uses <see cref="ICultureThreadScopeFactory"/> to create a scope that ensures
    /// <see cref="System.Globalization.CultureInfo.CurrentCulture"/> and <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
    /// are correctly set for the duration of the request.
    /// </summary>
    /// <remarks>
    /// Ensure this middleware is registered AFTER any culture-resolving middleware (e.g. reading cookies/headers)
    /// and BEFORE any middleware that consumes localization (e.g. MVC, Razor Pages).
    /// </remarks>
    public class CultureMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="CultureMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public CultureMiddleware(RequestDelegate next) => _next = next;

        /// <summary>
        /// Invokes the middleware logic.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="scopeFactory">Factory to create the culture scope.</param>
        public async Task InvokeAsync(HttpContext context, ICultureThreadScopeFactory scopeFactory)
        {
            // Wraps the entire request processing in a culture scope.
            // This ensures that any service resolved within this scope sees the correct culture.
            using (scopeFactory.BeginScope())
            {
                await _next(context);
            }
        }
    }
}