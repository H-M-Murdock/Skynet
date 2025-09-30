// C#

using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

// Führt mehrere Schritte aus; hebt erst danach den Level an.
public sealed class BarrierBootStep : IBootStep
{
    public RuntimeLevel MinLevel { get; }
    public RuntimeLevel TargetLevel { get; }
    private readonly IReadOnlyList<IBootStep> _steps;

    public BarrierBootStep(RuntimeLevel minLevel, RuntimeLevel targetLevel, IReadOnlyList<IBootStep> steps)
    {
        MinLevel = minLevel;
        TargetLevel = targetLevel;
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public async Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        foreach (var s in _steps)
        {
            if (s.MinLevel > MinLevel)
                throw new InvalidOperationException("Teil-Step verlangt höheren MinLevel als die Barrier.");
            await s.ExecuteAsync(services, ct).ConfigureAwait(false);
        }
    }
}
