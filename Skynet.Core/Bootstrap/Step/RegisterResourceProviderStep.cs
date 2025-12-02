using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Skynet.Core.ResourceProvider;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert das Resource-Provider-Subsystem.
/// Dies beinhaltet den Locator, die physischen Reader/Writer (FileSystem, Env)
/// sowie alle Converter, Serializer und Deserializer via Assembly-Scan.
/// </summary>
public class RegisterResourceProviderStep : IBootStep
{
    public string Name => "Register Resource Provider System";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // 1. Pfade sicher aus dem Context lesen
        // Diese müssen zuvor im InitFilesystemStep gesetzt worden sein.
        var rootPath = GetPathOrThrow(context, "Path:Root");
        var systemTenantPath = GetPathOrThrow(context, "Path:SystemTenant");
        var systemTempPath = GetPathOrThrow(context, "Path:SystemTemp");

        // 2. Core-Services registrieren
        context.Services.TryAddSingleton<IResourceLocator, ResourceLocator>();
        
        // TODO: DefaultResourceMaterializer registrieren, sobald fertig
        // context.Services.TryAddSingleton<IResourceMaterializer, DefaultResourceMaterializer>();

        // 3. Physische Provider Stack aufbauen
        RegisterPhysicalProviders(context.Services, rootPath, systemTenantPath, systemTempPath);

        // 4. Strategien scannen
        var strategyCount = RegisterStrategies(context.Services, typeof(IResourceLocator).Assembly);

        return Task.FromResult($"Registered Locator, Env/FS-Stack and {strategyCount} strategies.");
    }

    private string GetPathOrThrow(BootstrapContext context, string key)
    {
        if (context.Items.TryGetValue(key, out var obj) && obj is string path)
        {
            return path;
        }
        throw new InvalidOperationException($"ResourceProvider requires '{key}' to be set in BootstrapContext (check InitFilesystemStep).");
    }

    private void RegisterPhysicalProviders(
        IServiceCollection services, 
        string rootPath, 
        string systemTenantPath, 
        string systemTempPath)
    {
        /* 
         * ==========================================================================================
         * RESOURCE PROVIDER PRIORITY STACK (Lower Value = Higher Priority/Wins)
         * ==========================================================================================
         */

        // --- LEVEL 1: IN-MEMORY & RUNTIME (Die absolute Wahrheit) ---
        // Prio 10: Memory (Unit-Tests, Hot-Fixes zur Laufzeit)
        services.AddSingleton<IResourceReader>(new MemoryResourceReader(priority: 10));

        // --- LEVEL 2: ENVIRONMENT (Ops & Container overrides) ---
        // Prio 20: Process ENV (Docker ENV, CLI Parameter, CI/CD Secrets) -> Überschreibt User/Machine
        services.AddSingleton<IResourceReader>(new EnvironmentResourceReader(EnvScope.Process, priority: 20));

        // Prio 30: User ENV (Entwickler-Maschine spezifisch) -> Überschreibt Systemweite Einstellungen
        services.AddSingleton<IResourceReader>(new EnvironmentResourceReader(EnvScope.User, priority: 30));

        // Prio 40: Machine ENV (Server-Grundkonfiguration, Registry)
        services.AddSingleton<IResourceReader>(new EnvironmentResourceReader(EnvScope.Machine, priority: 40));

        // --- LEVEL 3: FILESYSTEM (Persistente Konfiguration) ---
        // Prio 100: Temp/Cache (Flüchtige Datei-Overrides)
        services.AddSingleton<IResourceReader>(sp => 
            new FileSystemResourceReader(systemTempPath, priority: 100));

        // Prio 110: System Tenant (Globale Defaults für alle Mandanten)
        services.AddSingleton<IResourceReader>(sp => 
            new FileSystemResourceReader(systemTenantPath, priority: 110));

        // Prio 120: Root Fallback (Basis-Installation)
        services.AddSingleton<IResourceReader>(sp => 
            new FileSystemResourceReader(rootPath, priority: 120));

        // --- WRITER ---
        // Wir schreiben standardmäßig in den Root-Pfad (oder man könnte SystemTenant wählen).
        // Der Writer braucht keine Prio für READ, aber für WRITE-Selection.
        services.AddSingleton<IResourceWriter>(sp => 
            new FileSystemResourceWriter(rootPath, priority: 100));
    }

    private int RegisterStrategies(IServiceCollection services, Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsInterface)
            .ToList();

        int count = 0;
        foreach (var type in types)
        {
            bool registered = false;
            // Helper local function or direct check
            if (type.IsAssignableTo(typeof(IResourceConverter))) { services.AddSingleton(typeof(IResourceConverter), type); registered = true; }
            if (type.IsAssignableTo(typeof(IResourceDeserializer))) { services.AddSingleton(typeof(IResourceDeserializer), type); registered = true; }
            if (type.IsAssignableTo(typeof(IResourceSerializer))) { services.AddSingleton(typeof(IResourceSerializer), type); registered = true; }
            if (registered) count++;
        }
        return count;
    }
}
