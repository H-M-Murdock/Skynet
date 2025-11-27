// "Starte die App. Wenn fertig, gib mir den ServiceProvider."

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skynet.Core.Bootstrap;

return await SkynetApp.RunAsync(async (sp) =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("App started successfully.");
    await Task.Delay(1000);
});