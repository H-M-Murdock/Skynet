// Skynet.Core/Logging/CallLogging/CallLoggingProxy.cs

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

/// <summary>
/// Ein dynamischer Proxy (basierend auf DispatchProxy), der Methodenaufrufe an ein Interface T abfängt.
/// Er dient als Middleware, um automatisch Logging-Events (Eingang, Ausgang, Exception) zu erzeugen,
/// ohne den eigentlichen Business-Code damit zu verschmutzen (AOP-Ansatz).
/// </summary>
/// <typeparam name="T">Das Interface, das proxied werden soll.</typeparam>
public class CallLoggingProxy<T> : DispatchProxy where T : class
{
    // Die eigentliche Implementierung der Geschäftslogik.
    public required T Target;
    // ServiceProvider, um Abhängigkeiten wie ILoggingClient "lazy" aufzulösen.
    public required IServiceProvider Sp;

    /// <summary>
    /// Factory-Methode zum Erstellen des Proxies.
    /// </summary>
    public static T Create(T target, IServiceProvider sp)
    {
        // DispatchProxy.Create erzeugt eine Instanz der Proxy-Klasse, die T implementiert.
        var p = Create<T, CallLoggingProxy<T>>() as CallLoggingProxy<T>;
        p!.Target = target ?? throw new ArgumentNullException(nameof(target));
        p!.Sp = sp ?? throw new ArgumentNullException(nameof(sp));
        return (p as T)!;
    }

    /// <summary>
    /// Die zentrale Methode, die JEDEN Aufruf an das Interface T abfängt.
    /// </summary>
    /// <param name="mi">Metadaten über die aufgerufene Methode.</param>
    /// <param name="args">Die übergebenen Argumente.</param>
    protected override object? Invoke(MethodInfo? mi, object?[]? args)
    {
        if (mi is null) return null;

        // 1. Prüfen, ob Logging für diese Methode überhaupt gewünscht ist.
        // Falls kein Attribut vorhanden ist, leiten wir den Aufruf einfach weiter (Performance).
        var logAttr = ResolveLogAttribute(mi);
        if (logAttr is null)
            return mi.Invoke(Target, args!);

        // Service Locator Pattern hier notwendig, da DispatchProxy keine Konstruktor-Injection unterstützt.
        // Der ILoggingClient übernimmt das eigentliche Schreiben/Puffern der Logs.
        var client = Sp.GetRequiredService<ILoggingClient>(); 
        var ct = CancellationToken.None;

        // Metadaten für das Log sammeln
        var op = $"{mi.DeclaringType?.FullName}.{mi.Name}";
        var cat = mi.DeclaringType?.FullName;
        // Tracing-Kontext (Distributed Tracing) auslesen, damit Logs korreliert werden können.
        var (traceId, spanId, corrId) = CollectTracing();
        var startTs = DateTimeOffset.UtcNow;

        // -------- ENTER (Loggen des Methoden-Eingangs)
        // Argumente werden nur serialisiert, wenn im Attribut explizit gewünscht (Datenschutz/Performance).
        var enterState = logAttr.LogParameters ? BuildParamState(mi, args) : Array.Empty<KeyValuePair<string, object?>>();
        var enterEvt = NewEvent(startTs, logAttr.LevelOnEnter, op, cat, null, null, corrId, traceId, spanId, enterState);
        
        // "FireAndForget": Wir warten nicht auf das Schreiben des Logs, um den Business-Call nicht zu bremsen.
        FireAndForget(client.LogAsync(enterEvt, ct)); 

        var sw = Stopwatch.StartNew();

        try
        {
            // 2. Den eigentlichen Methodenaufruf auf dem Target durchführen.
            var result = mi.Invoke(Target, args!);

            // 3. Asynchrone Rückgabetypen (Task, Task<T>, ValueTask, ValueTask<T>) behandeln.
            // Da 'Invoke' synchron zurückkehrt, aber einen Task zurückgibt, müssen wir uns
            // in diesen Task "einhängen" (await), um das Ende der Methode mitzubekommen.
            
            if (result is Task task)
                return HandleTaskAsync(mi, task, logAttr, client, ct, op, cat, corrId, traceId, spanId, sw, startTs, args);

            if (IsValueTask(mi.ReturnType))
                return HandleValueTaskAsync(mi, result!, logAttr, client, ct, op, cat, corrId, traceId, spanId, sw, startTs, args);

            // -------- EXIT (Synchroner Pfad)
            // Wenn wir hier sind, ist die Methode bereits fertig.
            var exitState = WithDuration(enterState, sw);
            if (logAttr.LogReturnValue)
                exitState = Append(exitState, new("Return", result));

            var exitEvt = NewEvent(DateTimeOffset.UtcNow, logAttr.LevelOnExit, op, cat, null, null, corrId, traceId, spanId, exitState);
            FireAndForget(client.LogAsync(exitEvt, ct));

            return result;
        }
        // TargetInvocationException ist der Wrapper von Reflection, wenn im Target eine Exception fliegt.
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            EmitException(client, ct, op, cat, logAttr.LevelOnException, tie.InnerException, corrId, traceId, spanId, sw);
            throw tie.InnerException; // Original-Exception weiterwerfen, damit der Stacktrace sauberer bleibt.
        }
        catch (Exception ex)
        {
            EmitException(client, ct, op, cat, logAttr.LevelOnException, ex, corrId, traceId, spanId, sw);
            throw;
        }
    }

    // Sucht das LogAttribute erst an der Methode, dann an der Klasse.
    private static LogCallAttribute? ResolveLogAttribute(MethodInfo mi)
        => mi.GetCustomAttribute<LogCallAttribute>(true)
        ?? mi.DeclaringType?.GetCustomAttribute<LogCallAttribute>(true);

    // Liest OpenTelemetry/Activity IDs aus.
    private static (string? trace, string? span, string? corr) CollectTracing()
    {
        var a = Activity.Current;
        return (a?.TraceId.ToString(), a?.SpanId.ToString(), a?.Id);
    }

    // Helper zum Erstellen des LogEvent-Objekts (MutableLogEvent).
    private static MutableLogEvent NewEvent(
        DateTimeOffset ts,
        LogLevel level,
        string operation,
        string? category,
        Exception? exObj,
        string? exMessage,
        string? corrId,
        string? traceId,
        string? spanId,
        IReadOnlyList<KeyValuePair<string, object?>> state)
    => new MutableLogEvent
    {
        Timestamp = ts,
        Level = level,
        Operation = operation,
        CategoryName = category,
        ExceptionObj = exObj,
        Exception = exMessage,
        CorrelationId = corrId,
        TraceId = traceId,
        SpanId = spanId,
        State = state // Wichtig: State wird hier als Liste übergeben für strukturierte Logs.
    };

    // Extrahiert Parameternamen und -werte mittels Reflection für das Logging.
    private static IReadOnlyList<KeyValuePair<string, object?>> BuildParamState(MethodInfo mi, object?[]? args)
    {
        if (args is null || args.Length == 0) return Array.Empty<KeyValuePair<string, object?>>();
        var ps = mi.GetParameters();
        var list = new List<KeyValuePair<string, object?>>(ps.Length);
        for (int i = 0; i < ps.Length; i++)
        {
            var name = ps[i].Name ?? $"arg{i}";
            list.Add(new KeyValuePair<string, object?>(name, args[i]));
        }
        return list;
    }

    // Fügt die Ausführungsdauer zum State hinzu.
    private static IReadOnlyList<KeyValuePair<string, object?>> WithDuration(
        IReadOnlyList<KeyValuePair<string, object?>> baseState,
        Stopwatch sw)
        => Append(baseState, new("DurationMs", sw.ElapsedMilliseconds));

    // Hilfsmethode zum Erweitern der Parameter-Liste (immutable-style aber mit neuer Liste).
    private static IReadOnlyList<KeyValuePair<string, object?>> Append(
        IReadOnlyList<KeyValuePair<string, object?>> baseState,
        KeyValuePair<string, object?> kv)
    {
        if (baseState.Count == 0) return new[] { kv };
        var list = new List<KeyValuePair<string, object?>>(baseState.Count + 1);
        list.AddRange(baseState);
        list.Add(kv);
        return list;
    }

    // Loggt eine Exception und misst die Zeit bis zum Fehler.
    private static void EmitException(
        ILoggingClient client,
        CancellationToken ct,
        string op,
        string? category,
        LogLevel level,
        Exception ex,
        string? corrId,
        string? traceId,
        string? spanId,
        Stopwatch sw)
    {
        var state = new[] { new KeyValuePair<string, object?>("DurationMs", sw.ElapsedMilliseconds) };
        var evt = NewEvent(DateTimeOffset.UtcNow, level, op, category, ex, ex.Message, corrId, traceId, spanId, state);
        FireAndForget(client.LogAsync(evt, ct));
    }

    // Stellt sicher, dass wir nicht auf den Log-Task warten, aber Compiler-Warnungen unterdrücken.
    private static void FireAndForget(ValueTask vt)
    {
        // Wenn der Task nicht sofort fertig ist (z.B. I/O Puffer voll), lassen wir ihn im Hintergrund laufen.
        if (!vt.IsCompletedSuccessfully) _ = vt.AsTask();
    }

    // ---------- Async Handler (Task/Task<T>) ----------
    // Diese Methoden sind notwendig, weil wir bei asynchronen Methoden nicht das Ergebnis des Aufrufs loggen können (es ist nur ein Task),
    // sondern wir müssen warten, bis der Task fertig ist.
    
    private static object HandleTaskAsync(
        MethodInfo mi,
        Task task,
        LogCallAttribute attr,
        ILoggingClient client,
        CancellationToken ct,
        string op,
        string? category,
        string? corrId,
        string? traceId,
        string? spanId,
        Stopwatch sw,
        DateTimeOffset startTs,
        object?[]? callArgs)
    {
        // Fallunterscheidung: Task (void) oder Task<T> (mit Rückgabewert)
        if (mi.ReturnType.IsGenericType && mi.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            // Es ist ein Task<T>. Wir kennen T hier zur Compilezeit nicht.
            // Trick: Wir rufen per Reflection die generische Methode HandleTaskGenericAsync auf.
            var resType = mi.ReturnType.GenericTypeArguments[0];
            var m = typeof(CallLoggingProxy<T>).GetMethod(nameof(HandleTaskGenericAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
            var g = m.MakeGenericMethod(resType);
            return g.Invoke(null, new object[] { task, attr, client, ct, op, category, corrId, traceId, spanId, sw, startTs, mi, callArgs })!;
        }
        
        // Es ist ein normaler Task (entspricht void async).
        return AwaitTask(task);

        // Lokale Funktion zum Awaiten
        async Task AwaitTask(Task t)
        {
            try
            {
                await t.ConfigureAwait(false);
                // Nach dem Await: Erfolgsfall loggen
                var baseState = attr.LogParameters ? BuildParamState(mi, callArgs) : Array.Empty<KeyValuePair<string, object?>>();
                var exitState = WithDuration(baseState, sw);
                var exitEvt = NewEvent(DateTimeOffset.UtcNow, attr.LevelOnExit, op, category, null, null, corrId, traceId, spanId, exitState);
                await client.LogAsync(exitEvt, ct);
            }
            catch (Exception ex)
            {
                // Exception im Task: Fehlerfall loggen
                EmitException(client, ct, op, category, attr.LevelOnException, ex, corrId, traceId, spanId, sw);
                throw; // Exception muss weitergeworfen werden, damit der Aufrufer sie bekommt.
            }
        }
    }

    // Generischer Handler für Task<TResult>. Wird per Reflection aufgerufen.
    private static async Task<TResult> HandleTaskGenericAsync<TResult>(
        Task t,
        LogCallAttribute attr,
        ILoggingClient client,
        CancellationToken ct,
        string op,
        string? category,
        string? corrId,
        string? traceId,
        string? spanId,
        Stopwatch sw,
        DateTimeOffset startTs,
        MethodInfo mi,
        object?[]? callArgs)
    {
        try
        {
            var real = (Task<TResult>)t;
            var result = await real.ConfigureAwait(false);

            // Hier haben wir Zugriff auf das echte Ergebnis (result) vom Typ TResult.
            var baseState = attr.LogParameters ? BuildParamState(mi, callArgs) : Array.Empty<KeyValuePair<string, object?>>();
            var exitState = WithDuration(baseState, sw);
            if (attr.LogReturnValue) exitState = Append(exitState, new("Return", result));

            var exitEvt = NewEvent(DateTimeOffset.UtcNow, attr.LevelOnExit, op, category, null, null, corrId, traceId, spanId, exitState);
            await client.LogAsync(exitEvt, ct);
            return result;
        }
        catch (Exception ex)
        {
            EmitException(client, ct, op, category, attr.LevelOnException, ex, corrId, traceId, spanId, sw);
            throw;
        }
    }

    // ---------- Async Handler (ValueTask/ValueTask<T>) ----------
    // Ähnliche Logik wie bei Task, aber speziell für den performanteren struct-Typ ValueTask.
    
    private static bool IsValueTask(Type t)
        => t == typeof(ValueTask) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ValueTask<>));

    private static object HandleValueTaskAsync(
        MethodInfo mi,
        object valueTaskObj,
        LogCallAttribute attr,
        ILoggingClient client,
        CancellationToken ct,
        string op,
        string? category,
        string? corrId,
        string? traceId,
        string? spanId,
        Stopwatch sw,
        DateTimeOffset startTs,
        object?[]? callArgs)
    {
        var type = mi.ReturnType;
        if (type == typeof(ValueTask))
        {
            return AwaitVT((ValueTask)valueTaskObj);

            async ValueTask AwaitVT(ValueTask vt)
            {
                try
                {
                    await vt.ConfigureAwait(false);
                    var baseState = attr.LogParameters ? BuildParamState(mi, callArgs) : Array.Empty<KeyValuePair<string, object?>>();
                    var exitState = WithDuration(baseState, sw);
                    var exitEvt = NewEvent(DateTimeOffset.UtcNow, attr.LevelOnExit, op, category, null, null, corrId, traceId, spanId, exitState);
                    await client.LogAsync(exitEvt, ct);
                }
                catch (Exception ex)
                {
                    EmitException(client, ct, op, category, attr.LevelOnException, ex, corrId, traceId, spanId, sw);
                    throw;
                }
            }
        }

        // ValueTask<T> Handling
        var resType = type.GenericTypeArguments[0];
        var m = typeof(CallLoggingProxy<T>).GetMethod(nameof(AwaitVTGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var g = m.MakeGenericMethod(resType);
        return g.Invoke(null, new object[] { valueTaskObj, attr, client, ct, op, category, corrId, traceId, spanId, sw, startTs, mi, callArgs })!;
    }

    private static async ValueTask<TResult> AwaitVTGeneric<TResult>(
        object valueTaskObj,
        LogCallAttribute attr,
        ILoggingClient client,
        CancellationToken ct,
        string op,
        string? category,
        string? corrId,
        string? traceId,
        string? spanId,
        Stopwatch sw,
        DateTimeOffset startTs,
        MethodInfo mi,
        object?[]? callArgs)
    {
        try
        {
            var vt = (ValueTask<TResult>)valueTaskObj;
            var result = await vt.ConfigureAwait(false);

            var baseState = attr.LogParameters ? BuildParamState(mi, callArgs) : Array.Empty<KeyValuePair<string, object?>>();
            var exitState = WithDuration(baseState, sw);
            if (attr.LogReturnValue) exitState = Append(exitState, new("Return", result));

            var exitEvt = NewEvent(DateTimeOffset.UtcNow, attr.LevelOnExit, op, category, null, null, corrId, traceId, spanId, exitState);
            await client.LogAsync(exitEvt, ct);
            return result;
        }
        catch (Exception ex)
        {
            EmitException(client, ct, op, category, attr.LevelOnException, ex, corrId, traceId, spanId, sw);
            throw;
        }
    }
}