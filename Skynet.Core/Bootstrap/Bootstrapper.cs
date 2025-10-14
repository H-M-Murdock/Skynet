// Skynet.Boot/Bootstrapper.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Bootstrap;

public sealed class Bootstrapper
{
    public RuntimeLevel CurrentLevel { get; private set; } = RuntimeLevel.Bootstrap;

    private bool _diLoggerReady = false;
    private ILogger? _logger;
    private bool _switchNoticePrinted = false;

    private void Log(IServiceProvider? sp, LogLevel level, string message)
    {
        if (!_diLoggerReady && sp is not null)
        {
            try
            {
                var factory = sp.GetService<ILoggerFactory>();
                if (factory is not null)
                {
                    _logger = factory.CreateLogger("Bootstrapper");
                    _diLoggerReady = true;

                    if (!_switchNoticePrinted)
                    {
                        // Einmalige Hinweis-Ausgabe auf die Konsole beim Wechsel
                        Console.WriteLine($"[{DateTime.UtcNow:O}] Switching to DI logging …");
                        _switchNoticePrinted = true;
                    }
                }
            }
            catch
            {
                // stay on console
            }
        }

        if (_diLoggerReady && _logger is not null)
            _logger.Log(level, "{Message}", message);
        else
            Console.WriteLine($"[{DateTime.UtcNow:O}] {message}");
    }
    public async Task<IServiceProvider> RunAsync(
        IEnumerable<IBootStep> steps,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var barriers = steps.OfType<BarrierBootStep>().ToList();
        if (barriers.Count == 0)
            throw new InvalidOperationException("Es wurden keine BarrierBootSteps übergeben.");

        var dupGroup = barriers.GroupBy(b => b.TargetLevel).FirstOrDefault(g => g.Count() > 1);
        if (dupGroup is not null)
        {
            var targets = string.Join(", ", dupGroup.Select(b => b.GetType().Name));
            throw new InvalidOperationException($"Mehr als eine Barrier für TargetLevel={dupGroup.Key}: {targets}");
        }

        var ordered = barriers.OrderBy(b => b.TargetLevel).ToList();

        // Eine ServiceCollection pro Barrier-Phase, nach jeder Barrier bauen wir einen Provider
        ServiceProvider? lastProvider = null;
        var services = new ServiceCollection();

        foreach (var barrier in ordered)
        {
            ct.ThrowIfCancellationRequested();

            if (barrier.MinLevel != CurrentLevel)
                throw new InvalidOperationException(
                    $"BarrierBootStep erwartet MinLevel={barrier.MinLevel}, aktueller Level ist {CurrentLevel}.");

            if (barrier.TargetLevel <= CurrentLevel)
                throw new InvalidOperationException(
                    $"BarrierBootStep TargetLevel={barrier.TargetLevel} muss größer als CurrentLevel={CurrentLevel} sein.");

            Log(lastProvider, LogLevel.Information, $"Starting barrier {barrier.GetType().Name} ({CurrentLevel} -> {barrier.TargetLevel})");

            // Teil-Schritte ausführen (registrieren in 'services')
            var innerSteps = barrier.GetInnerSteps();
            foreach (var step in innerSteps)
            {
                ct.ThrowIfCancellationRequested();
                Log(lastProvider, LogLevel.Information, $" Starting step {step.GetType().Name}");
                await step.ExecuteAsync(services, ct).ConfigureAwait(false);

                // Optionaler Report
                if (step is IStepReport reporter)
                {
                    var report = reporter.GetReport();
                    if (!string.IsNullOrWhiteSpace(report))
                        Log(lastProvider, LogLevel.Information, $"  Report: {report}");
                }

                Log(lastProvider, LogLevel.Information, $" Started  step {step.GetType().Name}");
            }

            // Provider für diese Barrier bauen
            var provider = services.BuildServiceProvider(validateScopes: true);

            // Level erhöhen und ab jetzt DI-Logger nutzen (Umschaltung erfolgt in Log(...) automatisch)
            CurrentLevel = barrier.TargetLevel;
            Log(provider, LogLevel.Information, $"Started  barrier {barrier.GetType().Name} (CurrentLevel={CurrentLevel})");

            // Vorherigen Provider entsorgen (saubere Lifetimes), neue ServiceCollection für nächste Barrier
            if (lastProvider is not null)
            {
                await lastProvider.DisposeAsync().ConfigureAwait(false);
            }
            lastProvider = provider;
        }

        // Finaler Provider ist der von der letzten Barrier
        var finalProvider = lastProvider ?? new ServiceCollection().BuildServiceProvider(validateScopes: true);
        Log(finalProvider, LogLevel.Information, $"Bootstrap finished. CurrentLevel={CurrentLevel}");

        return finalProvider;
    }
}