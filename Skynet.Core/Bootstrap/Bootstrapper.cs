// C#

using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

public sealed class Bootstrapper
{
    public RuntimeLevel CurrentLevel { get; private set; } = RuntimeLevel.Bootstrap;

    public async Task<IServiceProvider> RunAsync(
        IEnumerable<IBootStep> steps,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        // Nur BarrierBootSteps zulassen (wie besprochen)
        var barriers = steps.OfType<BarrierBootStep>().ToList();
        if (barriers.Count == 0)
            throw new InvalidOperationException("Es wurden keine BarrierBootSteps übergeben.");

        // Sicherstellen: pro TargetLevel genau eine Barrier
        var dupGroup = barriers.GroupBy(b => b.TargetLevel).FirstOrDefault(g => g.Count() > 1);
        if (dupGroup is not null)
        {
            var targets = string.Join(", ", dupGroup.Select(b => b.GetType().Name));
            throw new InvalidOperationException($"Mehr als eine Barrier für TargetLevel={dupGroup.Key}: {targets}");
        }

        // In deterministischer Reihenfolge nach TargetLevel ausführen
        var ordered = barriers.OrderBy(b => b.TargetLevel).ToList();

        var services = new ServiceCollection();

        foreach (var barrier in ordered)
        {
            ct.ThrowIfCancellationRequested();

            // Validierung: MinLevel muss dem aktuellen Level entsprechen
            if (barrier.MinLevel != CurrentLevel)
                throw new InvalidOperationException(
                    $"BarrierBootStep erwartet MinLevel={barrier.MinLevel}, aktueller Level ist {CurrentLevel}.");

            // Validierung: TargetLevel muss höher sein als CurrentLevel
            if (barrier.TargetLevel <= CurrentLevel)
                throw new InvalidOperationException(
                    $"BarrierBootStep TargetLevel={barrier.TargetLevel} muss größer als CurrentLevel={CurrentLevel} sein.");

            // Ausführen der Teil-Schritte
            await barrier.ExecuteAsync(services, ct).ConfigureAwait(false);

            // Nach erfolgreicher Barrier: Level anheben
            CurrentLevel = barrier.TargetLevel;
        }

        return services.BuildServiceProvider(validateScopes: true);
    }
}
