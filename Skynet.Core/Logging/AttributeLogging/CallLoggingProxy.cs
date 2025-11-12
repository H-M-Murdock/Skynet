// Skynet.Core/Logging/CallLogging/CallLoggingProxy.cs

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

internal sealed class CallLoggingProxy<T> : DispatchProxy where T : class
{
    internal required T Target;
    internal required IServiceProvider Sp;

    public static T Create(T target, IServiceProvider sp)
    {
        var p = Create<T, CallLoggingProxy<T>>() as CallLoggingProxy<T>;
        p!.Target = target ?? throw new ArgumentNullException(nameof(target));
        p!.Sp = sp ?? throw new ArgumentNullException(nameof(sp));
        return (p as T)!;
    }

    protected override object? Invoke(MethodInfo? mi, object?[]? args)
    {
        if (mi is null) return null;

        var logAttr = ResolveLogAttribute(mi);
        if (logAttr is null)
            return mi.Invoke(Target, args!);

        var client = Sp.GetRequiredService<ILoggingClient>(); // sendet selbst durch Enricher/Redaction usw. :contentReference[oaicite:2]{index=2}
        var ct = CancellationToken.None;

        var op = $"{mi.DeclaringType?.FullName}.{mi.Name}";
        var cat = mi.DeclaringType?.FullName;
        var (traceId, spanId, corrId) = CollectTracing();
        var startTs = DateTimeOffset.UtcNow;

        // -------- ENTER
        var enterState = logAttr.LogParameters ? BuildParamState(mi, args) : Array.Empty<KeyValuePair<string, object?>>();
        var enterEvt = NewEvent(startTs, logAttr.LevelOnEnter, op, cat, null, null, corrId, traceId, spanId, enterState);
        FireAndForget(client.LogAsync(enterEvt, ct)); // bewusst nicht blockieren

        var sw = Stopwatch.StartNew();

        try
        {
            var result = mi.Invoke(Target, args!);

            // Async-Pfade
            if (result is Task task)
                return HandleTaskAsync(mi, task, logAttr, client, ct, op, cat, corrId, traceId, spanId, sw, startTs, args);

            if (IsValueTask(mi.ReturnType))
                return HandleValueTaskAsync(mi, result!, logAttr, client, ct, op, cat, corrId, traceId, spanId, sw, startTs, args);

            // -------- EXIT (sync)
            var exitState = WithDuration(enterState, sw);
            if (logAttr.LogReturnValue)
                exitState = Append(exitState, new("Return", result));

            var exitEvt = NewEvent(DateTimeOffset.UtcNow, logAttr.LevelOnExit, op, cat, null, null, corrId, traceId, spanId, exitState);
            FireAndForget(client.LogAsync(exitEvt, ct));

            return result;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            EmitException(client, ct, op, cat, logAttr.LevelOnException, tie.InnerException, corrId, traceId, spanId, sw);
            throw tie.InnerException;
        }
        catch (Exception ex)
        {
            EmitException(client, ct, op, cat, logAttr.LevelOnException, ex, corrId, traceId, spanId, sw);
            throw;
        }
    }

    private static LogCallAttribute? ResolveLogAttribute(MethodInfo mi)
        => mi.GetCustomAttribute<LogCallAttribute>(true)
        ?? mi.DeclaringType?.GetCustomAttribute<LogCallAttribute>(true);

    private static (string? trace, string? span, string? corr) CollectTracing()
    {
        var a = Activity.Current;
        return (a?.TraceId.ToString(), a?.SpanId.ToString(), a?.Id);
    }

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
        State = state // Beachtung: IReadOnlyList-Setter. :contentReference[oaicite:3]{index=3}
    };

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

    private static IReadOnlyList<KeyValuePair<string, object?>> WithDuration(
        IReadOnlyList<KeyValuePair<string, object?>> baseState,
        Stopwatch sw)
        => Append(baseState, new("DurationMs", sw.ElapsedMilliseconds));

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

    private static void FireAndForget(ValueTask vt)
    {
        // ILoggingClient.LogAsync ist Best-Effort; wir blocken nicht.
        if (!vt.IsCompletedSuccessfully) _ = vt.AsTask();
    }

    // ---------- Async Handler (Task/Task<T>) ----------
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
        if (mi.ReturnType.IsGenericType && mi.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resType = mi.ReturnType.GenericTypeArguments[0];
            var m = typeof(CallLoggingProxy<T>).GetMethod(nameof(HandleTaskGenericAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
            var g = m.MakeGenericMethod(resType);
            return g.Invoke(null, new object[] { task, attr, client, ct, op, category, corrId, traceId, spanId, sw, startTs, mi, callArgs })!;
        }
        return AwaitTask(task);

        async Task AwaitTask(Task t)
        {
            try
            {
                await t.ConfigureAwait(false);
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

        // ValueTask<T>
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
