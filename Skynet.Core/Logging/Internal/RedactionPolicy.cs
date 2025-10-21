using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Skynet.Core.Logging;

/// <summary>
/// Policy zum Maskieren sensibler State-Werte anhand von Key-Regeln.
/// - Unterstützt exakte Keys, Prefix/Suffix und Regex.
/// - Idempotent: bereits maskierte Werte werden nicht erneut verändert.
/// - Reagiert auf Optionsänderungen via IOptionsMonitor.
/// </summary>
public sealed class RedactionPolicy : IRedactionPolicy
{
    private volatile RedactionOptions _options;
    private readonly IDisposable? _reload;

    public RedactionPolicy(IOptionsMonitor<RedactionOptions> monitor)
    {
        _options = monitor.CurrentValue ?? new RedactionOptions();
        _reload = monitor.OnChange(o => _options = o ?? new RedactionOptions());
    }

    public RedactionPolicy(IOptions<RedactionOptions> options)
        : this(new StaticMonitor(options?.Value ?? new RedactionOptions()))
    {
    }

    public void Redact(ILogEvent evt)
    {
        var opts = _options;
        if (evt.State is null || evt.State.Count == 0) return;

        // Früh exit, wenn keine Regeln
        var hasRules =
            (opts.Keys?.Count > 0) ||
            (opts.Prefixes?.Count > 0) ||
            (opts.Suffixes?.Count > 0) ||
            (opts.RegexPatterns?.Count > 0);

        if (!hasRules) return;

        var mask = opts.Mask ?? "***";

        // lokale Kopie nur erzeugen, wenn Änderungen nötig sind
        List<KeyValuePair<string, object?>>? edited = null;

        for (int i = 0; i < evt.State.Count; i++)
        {
            var (key, value) = evt.State[i];

            if (value is null)
                continue; // nichts zu maskieren

            // Idempotenz: bereits maskiert?
            if (value is string s && s == mask)
                continue;

            if (!ShouldRedact(key, value, opts))
                continue;

            edited ??= new List<KeyValuePair<string, object?>>(evt.State);
            edited[i] = new KeyValuePair<string, object?>(key, mask);
        }

        if (edited is not null && evt is MutableLogEvent m)
        {
            m.State = edited;
        }
    }

    private static bool ShouldRedact(string key, object? value, RedactionOptions o)
    {
        if (o.Keys is { Count: > 0 } keys)
        {
            foreach (var k in keys)
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return true;
        }

        if (o.Prefixes is { Count: > 0 } prefs)
        {
            foreach (var p in prefs)
                if (!string.IsNullOrEmpty(p) && key.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
        }

        if (o.Suffixes is { Count: > 0 } sufs)
        {
            foreach (var s in sufs)
                if (!string.IsNullOrEmpty(s) && key.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    return true;
        }

        if (o.RegexPatterns is { Count: > 0 } pats)
        {
            string? valueStr = value as string;
            foreach (var pat in pats)
            {
                if (string.IsNullOrWhiteSpace(pat)) continue;
                var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
                if (Regex.IsMatch(key, pat, options)) return true;
                if (valueStr is not null && Regex.IsMatch(valueStr, pat, options)) return true;
            }
        }

        return false;
    }

    private sealed class StaticMonitor : IOptionsMonitor<RedactionOptions>
    {
        private RedactionOptions _value;
        public StaticMonitor(RedactionOptions value) => _value = value;
        public RedactionOptions CurrentValue => _value;
        public RedactionOptions Get(string? name) => _value;
        public IDisposable OnChange(Action<RedactionOptions, string> listener) => new Nop();
        private sealed class Nop : IDisposable { public void Dispose() { } }
    }
}