// C#
namespace Skynet.Core.Bootstrap.Steps;

public interface IStepReport
{
    // Liefert eine kurze, menschenlesbare Statuszeile für Logs.
    string GetReport();
}
