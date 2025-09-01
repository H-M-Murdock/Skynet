# Skynet.Core.Localization – How to Use

This module provides basic localization primitives:

- `ILocalizationStore` + `InMemoryLocalizationStore`
- `ICurrentCultureProvider` + `CurrentCultureProvider`
- `IDateTimeFormatter` + `DateTimeFormatter`
- `CultureThreadScope` + `ICultureThreadScopeFactory`

All services are DI-friendly and can be replaced with tenant-aware or persistent implementations later.

---

## 📦 Registration

Register localization services in your DI container:

```csharp
// Program.cs or Startup.cs
builder.Services.AddSkynetLocalizationCore(new LocalizationOptions
{
    DefaultCulture = "en-US",
    // Optional: restrict supported cultures
    SupportedCultures = new[] { "en-US", "de-DE", "en-GB" }
});

Console / Worker Services

Use CultureThreadScope to apply the current culture temporarily.
This ensures all DateTime formatting and resource lookups respect the selected culture,
and automatically restores the previous culture when disposed.

using var scope = services.CreateScope();
var store = scope.ServiceProvider.GetRequiredService<ILocalizationStore>();

// Set culture for this "operation"
store.SetCulture("de-DE");

var factory = scope.ServiceProvider.GetRequiredService<ICultureThreadScopeFactory>();

using (factory.BeginScope())
{
    var formatter = scope.ServiceProvider.GetRequiredService<IDateTimeFormatter>();
    var dt = new DateTime(2025, 7, 4, 13, 5, 0);

    Console.WriteLine(formatter.Format(dt, DateTimePattern.LongDateTime));
    // Output (de-DE): Freitag, 4. Juli 2025 13:05:00
}

// After dispose: original thread culture is restored


🌐 ASP.NET Core Middleware

For web apps, add the middleware so that each request runs in the correct culture.

// Example: read culture from a cookie before middleware
app.Use(async (ctx, next) =>
{
    var store = ctx.RequestServices.GetRequiredService<ILocalizationStore>();
    if (ctx.Request.Cookies.TryGetValue("lang", out var lang))
        store.SetCulture(lang);

    await next();
});

// This ensures CurrentCulture and CurrentUICulture are applied per request
app.UseSkynetCulture();

🧪 Unit Testing

Inject the store directly to simulate culture changes in tests:

var store = new InMemoryLocalizationStore();
store.SetCulture("en-GB");

var provider = new CurrentCultureProvider(store, new LocalizationOptions());
var formatter = new DateTimeFormatter(provider);

var dt = new DateTime(2025, 1, 2, 3, 4, 5);
var formatted = formatter.Format(dt, DateTimePattern.ShortDate);

// en-GB → "02/01/2025"
Assert.Equal("02/01/2025", formatted);

📝 Notes

Use IDateTimeFormatter for UI formatting only.
For persistence/logging prefer invariant/UTC formats ("O" or "u").

CultureThreadScope is safe for nesting: inner scopes restore properly.

Restrict supported cultures with SupportedCultures to avoid exotic or invalid inputs.

ILocalizationStore is pluggable — you can implement tenant-aware or user-specific versions.