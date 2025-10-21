// Skynet.Core.Bootstrap/Steps/BootstrapClockStep.cs

using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Time;
using Skynet.Core.Logging;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapClockStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private DateTime _observedUtc;
    private DateOnly _observedToday;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // Zum Zeitpunkt der Registrierung Zeit “messen” (nur fürs Reporting)
        _observedUtc = DateTime.UtcNow;
        _observedToday = DateOnly.FromDateTime(_observedUtc);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IStopwatch, Stopwatch>();

        // Factory für Scoped-Messungen mit Logging-Integration
        // Hinweis: ILoggingClient sollte bereits in einem vorherigen Bootstrap-Step registriert sein.
        services.AddSingleton<ScopedStopwatchFactory>(sp =>
            new ScopedStopwatchFactory(
                sp.GetRequiredService<IStopwatch>(),
                sp.GetRequiredService<ILoggingClient>()));

        return Task.CompletedTask;
    }

    public string GetReport()
        => $"UTC now (at step): {_observedUtc:O}, Today: {_observedToday}";
}
