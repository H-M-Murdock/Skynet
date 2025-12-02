namespace Skynet.Core.Bootstrap;

/// <summary>
/// Aktiviert das Logging in die bootstrap.log Datei, sobald das Dateisystem bereit ist.
/// </summary>
public class EnableBootstrapLoggingStep : IBootStep
{
    public string Name => "Enable Bootstrap File Logging";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // Ruft die Logik im Context auf, um Sink und Provider zu tauschen
        context.UseFileLogging();
        
        // Wir geben einen Status zurück, der dann bereits (auch) in die Datei geloggt wird
        return Task.FromResult("Bootstrap logging switched to file sink.");
    }
}
