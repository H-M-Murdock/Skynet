using System;
using System.Threading;
using System.Threading.Tasks;
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
        private sealed class TestLogEvent : ILogEvent
        {
            public DateTimeOffset Timestamp { get; init; }
            public string Level { get; init; } = "Information";
            public string MessageTemplate { get; init; } = "";
            public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
            public string? Exception { get; init; }
            public string? SourceContext { get; init; }
            public string? Operation { get; init; }
            public int? EventCode { get; init; }
            public string EventId { get; init; } = Guid.NewGuid().ToString("n");
            public string? CorrelationId { get; init; }
        }

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
            var client = new CapturingLoggingClient();
            var factory = new ScopedStopwatchFactory(sw, client);

            // Act
            using (factory.Start("OrderService.Process", level: "Information", properties: new() { ["key"] = 42 }))
            {
                Thread.Sleep(25);
            }

            // Assert
            Assert.Single(client.Events);
            var evt = client.Events[0];
            Assert.Equal("Information", evt.Level);
            Assert.Equal("Timing {Operation} took {ElapsedMs} ms", evt.MessageTemplate);
            Assert.Equal("OrderService.Process", evt.Operation);
            Assert.True(evt.Properties.TryGetValue("ElapsedMs", out var msObj) && msObj is double);
            Assert.True((double)msObj! >= 5.0); // Toleranz
        }

        [Fact]
        public void Factory_Throws_On_Null_Args()
        {
            // Arrange
            var sw = new Skynet.Core.Time.Stopwatch();
            var client = new CapturingLoggingClient();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => new ScopedStopwatchFactory(null!, client));
            Assert.Throws<ArgumentNullException>(() => new ScopedStopwatchFactory(sw, null!));
        }
    }
}
