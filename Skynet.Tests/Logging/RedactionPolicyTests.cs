using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class RedactionPolicyTests
{
    private static MutableLogEvent NewEvt(IReadOnlyList<KeyValuePair<string, object?>> state) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        EventId = new(0, "t"),
        GlobalEventId = Guid.NewGuid().ToString("n"),
        State = state
    };

    [Fact]
    public void Masks_Exact_Keys_CaseInsensitive()
    {
        var opts = Options.Create(new RedactionOptions
        {
            Keys = new() { "password", "apiKey" },
            Mask = "***"
        });

        var pol = new RedactionPolicy(opts);
        var evt = NewEvt(new List<KeyValuePair<string, object?>>
        {
            new("Password", "secret"),
            new("user", "ada"),
            new("ApiKey", "k-123")
        });

        pol.Redact(evt);

        Assert.Equal("***", evt.State[0].Value);
        Assert.Equal("ada", evt.State[1].Value);
        Assert.Equal("***", evt.State[2].Value);
    }

    [Fact]
    public void Masks_By_Prefix_And_Suffix()
    {
        var opts = Options.Create(new RedactionOptions
        {
            Prefixes = new() { "hdr_" /*, "meta-"*/ },
            Suffixes = new() { "_secret", "-token" },
            Mask = "███"
        });

        var pol = new RedactionPolicy(opts);
        var evt = NewEvt(new List<KeyValuePair<string, object?>>
        {
            new("hdr_auth", "x"),
            new("name_secret", "y"),
            new("meta-trace", "z"),
            new("id-token", "t"),
            new("plain", "p")
        });

        pol.Redact(evt);

        Assert.Equal("███", evt.State[0].Value); // prefix hdr_
        Assert.Equal("███", evt.State[1].Value); // suffix _secret
        Assert.Equal("z", evt.State[2].Value);   // meta-trace: bleibt
        Assert.Equal("███", evt.State[3].Value); // suffix -token
        Assert.Equal("p", evt.State[4].Value);
    }

    [Fact]
    public void Regex_Patterns_Are_Applied()
    {
        var opts = Options.Create(new RedactionOptions
        {
            RegexPatterns = new() { "^cc_\\d+$", "jwt|bearer" },
            Mask = "[redacted]"
        });

        var pol = new RedactionPolicy(opts);
        var evt = NewEvt(new List<KeyValuePair<string, object?>>
        {
            new("cc_1", "4111 1111 1111 1111"),
            new("auth", "bearer abc"),
            new("user", "bob")
        });

        pol.Redact(evt);

        Assert.Equal("[redacted]", evt.State[0].Value);
        Assert.Equal("[redacted]", evt.State[1].Value);
        Assert.Equal("bob", evt.State[2].Value);
    }

    [Fact]
    public void Is_Idempotent()
    {
        var opts = Options.Create(new RedactionOptions
        {
            Keys = new() { "secret" },
            Mask = "***"
        });

        var pol = new RedactionPolicy(opts);
        var evt = NewEvt(new List<KeyValuePair<string, object?>>
        {
            new("secret", "value"),
            new("other", "v")
        });

        pol.Redact(evt);
        pol.Redact(evt);

        Assert.Equal("***", evt.State[0].Value);
        Assert.Equal("v", evt.State[1].Value);
    }

    [Fact]
    public void Options_Change_Is_Picked_Up()
    {
        var monitor = new OptionsMonitorFake<RedactionOptions>(new()
        {
            Keys = new() { "a" },
            Mask = "X"
        });
        var pol = new RedactionPolicy(monitor);

        var evt = NewEvt(new List<KeyValuePair<string, object?>>
        {
            new("a", "1"),
            new("b", "2")
        });

        pol.Redact(evt);
        Assert.Equal("X", evt.State[0].Value);
        Assert.Equal("2", evt.State[1].Value);

        monitor.Update(new RedactionOptions { Keys = new() { "b" }, Mask = "Y" });

        pol.Redact(evt);
        Assert.Equal("X", evt.State[0].Value); // bleibt maskiert
        Assert.Equal("Y", evt.State[1].Value); // neu maskiert
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
