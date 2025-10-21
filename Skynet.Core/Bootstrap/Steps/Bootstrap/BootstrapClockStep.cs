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

        // Lazy-Factory: holt ILoggingClient erst beim Dispose eines Scopes aus dem Provider (Best Effort).
        services.AddSingleton<ScopedStopwatchFactory>(sp =>
            new LazyScopedStopwatchFactory(sp, sp.GetRequiredService<IStopwatch>()));

        return Task.CompletedTask;
    }

    public string GetReport()
        => $"UTC now (at step): {_observedUtc:O}, Today: {_observedToday}";
}

