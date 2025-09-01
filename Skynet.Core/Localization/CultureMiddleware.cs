using Microsoft.AspNetCore.Http;

namespace Skynet.Core.Localization
{
    public class CultureMiddleware
    {
        private readonly RequestDelegate _next;

        public CultureMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, ICultureThreadScopeFactory scopeFactory)
        {
            using (scopeFactory.BeginScope())
            {
                await _next(context);
            }
        }
    }
}