// C#

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapConfigStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = "no config loaded";

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        var baseDir = Directory.GetCurrentDirectory();
        var jsonPath = Path.Combine("bootstrap", "appsettings.bootstrap.json");
        var jsonFull = Path.Combine(baseDir, jsonPath);
        var jsonExists = File.Exists(jsonFull);

        var builder = new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile(path: jsonPath, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "SKYNET_");

        var config = builder.Build();
        services.AddSingleton(config);

        // Klarer Report
        _report = jsonExists
            ? $"config: json='{jsonPath}' loaded, envPrefix='SKYNET_'"
            : $"config: no json ('{jsonPath}' not found), envPrefix='SKYNET_'";

        return Task.CompletedTask;
    }

    public string GetReport() => _report;
}
