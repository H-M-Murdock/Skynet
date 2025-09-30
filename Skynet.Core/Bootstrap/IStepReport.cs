// C#
namespace Skynet.Core.Bootstrap;

public interface IStepReport
{
    // Liefert eine kurze, menschenlesbare Statuszeile für Logs.
    string GetReport();
}
