using System;
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Bootstrap;
using Skynet.Core.Logging;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Integrations-Test: DI-Registrierung durch BootstrapClockStep.
    /// </summary>
    public class BootstrapClockStepDiTests
    {
        private sealed class DummyLoggingClient : ILoggingClient
        {
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask LogAsync(ILogEvent evt, CancellationToken ct) => ValueTask.CompletedTask;
            public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(bool drain, CancellationToken ct) => Task.CompletedTask;
            public long DroppedCount => 0;
            public int QueueLength => 0;
            public int QueueCapacity => 1024;
        }

        [Fact]
        public async Task BootstrapClockStep_Registers_Clock_Stopwatch_And_ScopedFactory()
        {
            // Arrange
            var services = new ServiceCollection();
            // ILoggingClient muss vorhanden sein, da BootstrapClockStep ScopedStopwatchFactory registriert, die es benötigt
            services.AddSingleton<ILoggingClient, DummyLoggingClient>();
            var step = new BootstrapClockStep();

            // Act
            await step.ExecuteAsync(services, CancellationToken.None);
            var provider = services.BuildServiceProvider();

            // Assert
            var clock = provider.GetRequiredService<IClock>();
            var mono = provider.GetRequiredService<IStopwatch>();
            var factory = provider.GetRequiredService<ScopedStopwatchFactory>();

            Assert.IsType<SystemClock>(clock);
            Assert.IsType<Skynet.Core.Time.Stopwatch>(mono);
            Assert.NotNull(factory);

            // sanity: Factory verwendbar
            using (factory.Start("Bootstrap.Test")) { }
        }
    }
}
