// ... existing code ...
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Time
{
    /// <summary>
    /// Tests für die Logging-Integration der ScopedStopwatchFactory.
    /// </summary>
    public class ScopedStopwatchLoggingTests
    {
        private sealed class CapturingLoggingClient : ILoggingClient
        {
            public readonly List<ILogEvent> Events = new();
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask LogAsync(ILogEvent evt, CancellationToken ct)
            {
                Events.Add(evt);
                return ValueTask.CompletedTask;
            }
            public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(bool drain, CancellationToken ct) => Task.CompletedTask;
            public long DroppedCount => 0;
            public int QueueLength => Events.Count;
            public int QueueCapacity => 1024;
        }

        [Fact]
        public async Task Factory_Creates_Scope_That_Logs_On_Dispose()
        {
            // Arrange
            var sw = new Skynet.Core.Time.Stopwatch();
            var services = new ServiceCollection();
            var client = new CapturingLoggingClient();
            services.AddSingleton<ILoggingClient>(client);
            var sp = services.BuildServiceProvider();
            var factory = new ScopedStopwatchFactory(sw, sp);

            // Act
            using (factory.Start("OrderService.Process", level: LogLevel.Information, properties: new() { ["key"] = 42 }))
            {
                Thread.Sleep(25);
            }

            // Assert
            Assert.Single(client.Events);
            var evt = client.Events[0];
            Assert.Equal(LogLevel.Information, evt.Level);
            Assert.Equal("OrderService.Process", evt.Operation);

            // Prüfe State-Inhalte
            // ElapsedMs vorhanden
            var elapsed = evt.State.FirstOrDefault(kv => kv.Key == "ElapsedMs").Value;
            Assert.IsType<double>(elapsed);
            Assert.True((double)elapsed! >= 5.0); // Toleranz

            // Operation gespiegelt
            var op = evt.State.FirstOrDefault(kv => kv.Key == "Operation").Value as string;
            Assert.Equal("OrderService.Process", op);
        }

        [Fact]
        public void Factory_Throws_On_Null_Stopwatch_Or_ServiceProvider()
        {
            // Arrange
            var sw = new Skynet.Core.Time.Stopwatch();
            var services = new ServiceCollection().BuildServiceProvider();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => new ScopedStopwatchFactory(null!, services));
            Assert.Throws<ArgumentNullException>(() => new ScopedStopwatchFactory(sw, null!));
        }
    }
}
// ... existing code ...