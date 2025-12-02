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

        // Teste Schreibrechte für Dateien im Root, aber hinterlasse keinen Müll
        EnsureWritable(Path.Combine(basePath, "bootstrap.pid"));
        EnsureFileExists(Path.Combine(basePath, "bootstrap.cfg"));

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

    // Umbenannt und Logik angepasst: Prüfen & Aufräumen
    private void EnsureFileExists(string path)
    {
        // Wenn die Datei schon existiert, gehen wir davon aus, dass sie okay ist (oder wir fassen sie nicht an)
        if (File.Exists(path)) return;

        try
        {
            // Erstellen
            File.WriteAllText(path, string.Empty);
            // Und sofort wieder löschen, damit der echte Logger ein frisches File erstellen kann
            File.Delete(path);
        }
        catch (Exception ex)
        {
             throw new UnauthorizedAccessException($"Access denied for file '{path}'.", ex);
        }
    }
    
    private void EnsureWritable(string path)
    {
        try
        {
            // Falls eine alte PID/Lock-Datei da ist -> weg damit
            if (File.Exists(path)) File.Delete(path);

            // Schreib-Test
            File.WriteAllText(path, "init_check");
                
            // Aufräumen
            File.Delete(path);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException($"Write check failed for '{path}'. Check filesystem permissions.", ex);
        }
    }
}
