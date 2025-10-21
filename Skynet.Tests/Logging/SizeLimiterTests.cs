using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class SizeLimiterTests
{
    private static MutableLogEvent NewEvt(params (string Key, object? Value)[] kvs)
        => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Information,
            EventId = new(0, "t"),
            GlobalEventId = Guid.NewGuid().ToString("n"),
            State = CreateState(kvs)
        };

    private static IReadOnlyList<KeyValuePair<string, object?>> CreateState(params (string Key, object? Value)[] kvs)
    {
        var list = new List<KeyValuePair<string, object?>>(kvs.Length);
        foreach (var kv in kvs) list.Add(new KeyValuePair<string, object?>(kv.Key, kv.Value));
        return list;
    }

    [Fact]
    public void Truncates_String_Values()
    {
        var opts = Options.Create(new SizeLimitOptions { MaxStringLength = 5, Ellipsis = "…" });
        var limiter = new SizeLimiter(opts);

        var evt = NewEvt(("a", "1234567"), ("b", 42), ("c", "ok"));
        limiter.Truncate(evt);

        Assert.Equal("1234…", evt.State[0].Value);
        Assert.Equal(42, evt.State[1].Value);
        Assert.Equal("ok", evt.State[2].Value);
    }

    [Fact]
    public void Limits_State_Entry_Count()
    {
        var opts = Options.Create(new SizeLimitOptions { MaxStateEntries = 2 });
        var limiter = new SizeLimiter(opts);

        var evt = NewEvt(("k1", 1), ("k2", 2), ("k3", 3));
        limiter.Truncate(evt);

        Assert.Equal(2, evt.State.Count);
        Assert.Equal("k1", evt.State[0].Key);
        Assert.Equal("k2", evt.State[1].Key);
    }

    [Fact]
    public void Caps_By_Approx_Total_Bytes()
    {
        var opts = Options.Create(new SizeLimitOptions { MaxTotalBytesApprox = 10, Ellipsis = "…" });
        var limiter = new SizeLimiter(opts);

        // Roughly: "k1"(2) + "123456"(6) ~ 8 + overhead -> second entry may be truncated/removed
        var evt = NewEvt(("k1", "123456"), ("k2", "ABCD"));
        limiter.Truncate(evt);

        // Either truncated first value or removal of second entry to stay under cap
        Assert.True(evt.State.Count is 1 or 2);
        var total = ApproxBytes(evt.State);
        Assert.True(total <= 10);
    }

    [Fact]
    public void Idempotent_Reapply_Has_No_Further_Effect()
    {
        var opts = Options.Create(new SizeLimitOptions { MaxStringLength = 3, MaxStateEntries = 2, Ellipsis = "…" });
        var limiter = new SizeLimiter(opts);

        var evt = NewEvt(("a", "abcdef"), ("b", "xyz"), ("c", "12345"));
        limiter.Truncate(evt);
        var first = Snapshot(evt.State);
        limiter.Truncate(evt);
        var second = Snapshot(evt.State);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Options_Changes_Are_Picked_Up()
    {
        var monitor = new OptionsMonitorFake<SizeLimitOptions>(new()
        {
            MaxStringLength = 100, MaxStateEntries = 10, MaxTotalBytesApprox = 0
        });
        var limiter = new SizeLimiter(monitor);

        var evt = NewEvt(("a", "0123456789XYZ")); // 13 chars
        limiter.Truncate(evt);
        Assert.Equal("0123456789XYZ", evt.State[0].Value);

        monitor.Update(new SizeLimitOptions { MaxStringLength = 5, Ellipsis = "…" });
        limiter.Truncate(evt);
        Assert.Equal("0123…", evt.State[0].Value);
    }

    private static int ApproxBytes(IReadOnlyList<KeyValuePair<string, object?>> state)
    {
        var sum = 0;
        foreach (var (k, v) in state)
        {
            sum += k.Length;
            if (v is string s) sum += s.Length;
            else if (v is not null) sum += 4; // rough
        }
        return sum;
    }

    private static string Snapshot(IReadOnlyList<KeyValuePair<string, object?>> state)
    {
        return string.Join("|", state.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private sealed class OptionsMonitorFake<T> : IOptionsMonitor<T> where T : class, new()
    {
        private T _current;
        private event Action<T, string>? _onChange;

        public OptionsMonitorFake(T current) => _current = current;

        public T CurrentValue => _current;

        public T Get(string? name) => _current;

        public IDisposable OnChange(Action<T, string> listener)
        {
            _onChange += listener;
            return new Unsub(() => _onChange -= listener);
        }

        public void Update(T value)
        {
            _current = value;
            _onChange?.Invoke(value, string.Empty);
        }

        private sealed class Unsub : IDisposable
        {
            private readonly Action _dispose;
            public Unsub(Action d) => _dispose = d;
            public void Dispose() => _dispose();
        }
    }
}
