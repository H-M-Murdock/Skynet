using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap; // Namespace angepasst auf Steps

public class InitFilesystemStep : IBootStep
{
    public string Name => "Initialize Filesystem Structure";

    private readonly string? _customEnvVar;
    private readonly string _rootFolderName;

    public InitFilesystemStep(string rootFolderName = "skynet", string? overrideEnvVar = null)
    {
        _rootFolderName = rootFolderName;
        _customEnvVar = overrideEnvVar;
    }

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        string basePath;

        // 1. Pfadermittlung
        if (!string.IsNullOrWhiteSpace(_customEnvVar) && 
            Environment.GetEnvironmentVariable(_customEnvVar) is { } envPath && 
            !string.IsNullOrWhiteSpace(envPath))
        {
            basePath = envPath;
            context.Logger.LogInformation($"Using Root Path from ENV '{_customEnvVar}': {basePath}");
        }
        else
        {
            basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _rootFolderName);
            context.Logger.LogInformation($"Using Default Root Path: {basePath}");
        }

        var systemPath = Path.Combine(basePath, "tenants", "system");

        var directories = new List<string>
        {
            // System Tenant
            Path.Combine(systemPath, "i18n"),
            Path.Combine(systemPath, "logging"),
            Path.Combine(systemPath, "config"),
            Path.Combine(systemPath, "assets"),
            Path.Combine(systemPath, "license"),
            
            // Runtime
            Path.Combine(systemPath, "temp"),
            Path.Combine(systemPath, "cache"),

            // Global Temp
            Path.Combine(basePath, "temp") 
        };

        foreach (var dir in directories)
        {
            EnsureDirectory(dir, context);
        }

        TouchFile(Path.Combine(basePath, "bootstrap.log"));
        TouchFile(Path.Combine(basePath, "bootstrap.cfg"));

        // --- ÄNDERUNG: Direkter Zugriff auf das Dictionary ---
        context.Items["Path:Root"] = basePath;
        context.Items["Path:SystemTenant"] = systemPath;
        context.Items["Path:SystemTemp"] = Path.Combine(systemPath, "temp");
        // -----------------------------------------------------

        return Task.FromResult($"Verified structure at {basePath}");
    }

    private void EnsureDirectory(string path, BootstrapContext context)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var probe = Path.Combine(path, $".probe_{Guid.NewGuid()}");
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException($"Access denied for directory '{path}'.", ex);
        }
    }

    private void TouchFile(string path)
    {
        if (!File.Exists(path)) File.WriteAllText(path, string.Empty);
    }
}
