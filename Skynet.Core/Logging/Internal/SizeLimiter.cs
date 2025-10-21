using System.Text;
using Microsoft.Extensions.Options;

namespace Skynet.Core.Logging;

/// <summary>
/// Erzwingt Größenlimits für Log-Events:
/// - Kürzt String-Werte auf MaxStringLength (mit Ellipsis).
/// - Begrenzt Anzahl der State-Einträge auf MaxStateEntries (FIFO-Prinzip).
/// - Hält eine grobe Obergrenze MaxTotalBytesApprox ein (Key/Value-Längen, UTF8 approximiert).
/// Idempotent und ChangeToken-fähig (IOptionsMonitor).
/// </summary>
public sealed class SizeLimiter : ISizeLimiter
{
    private volatile SizeLimitOptions _options;
    private readonly IDisposable? _reload;

    public SizeLimiter(IOptionsMonitor<SizeLimitOptions> monitor)
    {
        _options = monitor.CurrentValue ?? new SizeLimitOptions();
        _reload = monitor.OnChange(o => _options = o ?? new SizeLimitOptions());
    }

    public SizeLimiter(IOptions<SizeLimitOptions> options)
        : this(new StaticMonitor(options?.Value ?? new SizeLimitOptions()))
    {
    }

    public void Truncate(ILogEvent evt)
    {
        var opts = _options;
        if (evt is not MutableLogEvent m || evt.State is null || evt.State.Count == 0)
            return;

        var state = m.State as List<KeyValuePair<string, object?>> ?? new List<KeyValuePair<string, object?>>(evt.State);

        // 1) Kürze Strings
        if (opts.MaxStringLength > 0)
        {
            var ell = opts.Ellipsis ?? "…";
            for (int i = 0; i < state.Count; i++)
            {
                var (k, v) = state[i];
                if (v is string s && s.Length > opts.MaxStringLength)
                {
                    var cut = opts.MaxStringLength - ell.Length;
                    if (cut < 1) cut = 1;
                    var truncated = s.Substring(0, cut) + ell;
                    state[i] = new KeyValuePair<string, object?>(k, truncated);
                }
            }
        }

        // 2) Begrenzt Anzahl Einträge
        if (opts.MaxStateEntries > 0 && state.Count > opts.MaxStateEntries)
        {
            state.RemoveRange(opts.MaxStateEntries, state.Count - opts.MaxStateEntries);
        }

        // 3) Grobe Gesamtbytes
        if (opts.MaxTotalBytesApprox > 0)
        {
            EnsureApproxUnderCap(state, opts.MaxTotalBytesApprox);
        }

        m.State = state;
    }

    private static void EnsureApproxUnderCap(List<KeyValuePair<string, object?>> state, int cap)
    {
        int Approx()
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

        while (state.Count > 0 && Approx() > cap)
        {
            // Strategie: zuerst letzte Einträge entfernen (neuere, optional diskutierbar)
            state.RemoveAt(state.Count - 1);
        }
    }

    private sealed class StaticMonitor : IOptionsMonitor<SizeLimitOptions>
    {
        private SizeLimitOptions _value;
        public StaticMonitor(SizeLimitOptions value) => _value = value;
        public SizeLimitOptions CurrentValue => _value;
        public SizeLimitOptions Get(string? name) => _value;
        public IDisposable OnChange(Action<SizeLimitOptions, string> listener) => new Nop();
        private sealed class Nop : IDisposable { public void Dispose() { } }
    }
}