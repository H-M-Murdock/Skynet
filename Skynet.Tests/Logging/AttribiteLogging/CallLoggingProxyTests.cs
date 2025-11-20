using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;

namespace Skynet.Tests.Logging;

public class CallLoggingProxyTests
{
    /// <summary>
    /// Dieser Test prüft den kompletten Flow:
    /// Proxy -> LoggingClient -> InMemoryTransport -> LoggingServer -> MemoryLogSink.
    /// Er verifiziert, dass Argumente, Rückgabewerte und Exceptions korrekt geloggt werden.
    /// </summary>
    [Fact]
    public async Task Proxy_Should_Log_Enter_Exit_And_Exceptions_With_Correct_Data()
    {
        // 1. SETUP: Infrastruktur aufbauen (Server & Client via Memory verbunden)
        
        // Sink: Hier landen die Logs am Ende
        var memorySink = new MemoryLogSink();
        var router = new SingleSinkRouter(memorySink);
        
        // Server: Empfängt Daten via InMemory-Listener
        var listener = new InMemoryEventListener();
        // Wir nutzen den NdjsonLogEventEncoder, da er im Context verfügbar ist
        var encoder = new NdjsonLogEventEncoder(); 
        var materializer = new DefaultLogEventMaterializer();
        
        var server = new LoggingServer(
            listener, 
            encoder, 
            router, 
            materializer,
            new LoggingServerOptions { PeriodicFlushInterval = TimeSpan.FromMilliseconds(50) }
        );
        await server.StartAsync(CancellationToken.None);

        // Client: Sendet an den Listener
        var transportFactory = new InMemoryEventTransportFactory(listener);
        var client = new LoggingClient(
            transportFactory, 
            encoder, 
            new SimpleBackpressurePolicy()
        );
        await client.StartAsync(CancellationToken.None);

        // ServiceProvider: Der Proxy benötigt Zugriff auf den ILoggingClient
        var services = new ServiceCollection();
        services.AddSingleton<ILoggingClient>(client);
        var sp = services.BuildServiceProvider();

        // Target & Proxy Erstellung
        ICalculator realService = new CalculatorService();
        ICalculator proxy = CallLoggingProxy<ICalculator>.Create(realService, sp);

        // 2. EXECUTION: Aufrufe gegen den Proxy tätigen

        // Szenario A: Erfolgreicher Async-Call mit Rückgabewert
        // Wir starten eine Activity, um zu prüfen, ob TraceIds mitkommen
        using (var activity = new Activity("TestTrace").Start())
        {
            var result = await proxy.AddAsync(10, 5);
            Assert.Equal(15, result);
        }

        // Szenario B: Exception-Call
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await proxy.FailAsync());

        // 3. TEARDOWN & FLUSH: Sicherstellen, dass alles verarbeitet wurde
        
        // Client leerlaufen lassen
        await client.FlushAsync(CancellationToken.None);
        // Kurz warten, damit der Server-Worker die Events abarbeitet (Server ist asynchron)
        await Task.Delay(200); 
        // Server stoppen (flusht Sinks)
        await server.StopAsync(CancellationToken.None);

        // 4. ASSERTION: Inhalt des Sinks prüfen

        var events = memorySink.Snapshot();
        
        // Wir erwarten 4 Events:
        // 1. AddAsync ENTER
        // 2. AddAsync EXIT (mit Return Value)
        // 3. FailAsync ENTER
        // 4. FailAsync EXCEPTION
        Assert.Equal(4, events.Count);

        // --- Prüfung Szenario A (AddAsync) ---
        var enterAdd = events[0];
        var exitAdd = events[1];

        // Änderung: Erwarte den Interface-Namen statt des Klassennamens
        // "Skynet.Tests.Logging.CallLoggingProxyTests+ICalculator.AddAsync"
        Assert.Equal($"{typeof(ICalculator).FullName}.{nameof(ICalculator.AddAsync)}", enterAdd.Operation);
        
        Assert.Equal(LogLevel.Debug, enterAdd.Level); // Default LevelOnEnter
        // Parameter prüfen (Enter)
        Assert.Contains(enterAdd.State, kv => kv.Key == "a" && kv.Value?.ToString() == "10");
        Assert.Contains(enterAdd.State, kv => kv.Key == "b" && kv.Value?.ToString() == "5");

        // Rückgabewert & Duration prüfen (Exit)
        Assert.Equal(LogLevel.Information, exitAdd.Level); // Configured LevelOnExit
        Assert.Contains(exitAdd.State, kv => kv.Key == "Return" && kv.Value?.ToString() == "15");
        Assert.Contains(exitAdd.State, kv => kv.Key == "DurationMs");

        // Tracing Context prüfen (muss gesetzt sein, da wir eine Activity hatten)
        Assert.NotNull(enterAdd.TraceId);
        Assert.Equal(enterAdd.TraceId, exitAdd.TraceId);

        // --- Prüfung Szenario B (FailAsync) ---
        var enterFail = events[2];
        var errorFail = events[3];

        // Auch hier: Interface-Name
        Assert.Equal($"{typeof(ICalculator).FullName}.{nameof(ICalculator.FailAsync)}", errorFail.Operation);
        
        Assert.Equal(LogLevel.Error, errorFail.Level); // Default LevelOnException

        // Cleanup
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    // --- Dummy Service Definitionen ---

    // Interface muss Attribute tragen (oder Implementation, Proxy schaut auf beides)
    public interface ICalculator
    {
        [LogCall(LogParameters = true, LogReturnValue = true, LevelOnExit = LogLevel.Information)]
        Task<int> AddAsync(int a, int b);

        [LogCall]
        Task FailAsync();
    }

    public class CalculatorService : ICalculator
    {
        public async Task<int> AddAsync(int a, int b)
        {
            await Task.Delay(10); // Simulierte Arbeit
            return a + b;
        }

        public async Task FailAsync()
        {
            await Task.Delay(10);
            throw new InvalidOperationException("Boom!");
        }
    }
}
