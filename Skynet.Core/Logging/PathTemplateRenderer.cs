namespace Skynet.Core.Logging;

// Rendert Pfad-Templates wie "logs/{tenant}/{component}/{yyyy-MM-dd}.log".
// Konvention: Properties["TenantId"], Properties["Component"] werden genutzt, falls vorhanden.
public interface IPathTemplateRenderer
{
    string Render(ILogEvent evt, DateTimeOffset now);
}
