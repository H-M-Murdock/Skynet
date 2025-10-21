using System.Text;

namespace Skynet.Core.Logging;

/// <summary>
/// Schlanker, schemaloser Text-Formatter für ILogEvent:
/// Format:
///   ts level [Cat=... EventId=Id(Name) GEID=... Op=... Corr=... Trace=... Span=...] statePairs ex=...
/// - Timestamp: UTC oder lokal, ISO-8601 "O".
/// - State: key=value durch Leerzeichen getrennt (optional).
/// - Keine Template-Auflösung; State wird direkt ausgegeben.
/// </summary>
public sealed class SimpleLineLogTextFormatter : ILogTextFormatter
{
    private readonly bool _useUtcTimestamps;
    private readonly bool _includeState;

    public SimpleLineLogTextFormatter(bool useUtcTimestamps = true, bool includeState = true)
    {
        _useUtcTimestamps = useUtcTimestamps;
        _includeState = includeState;
    }

    public string Format(ILogEvent evt)
    {
        var sb = new StringBuilder(256);

        // Timestamp
        var ts = _useUtcTimestamps ? evt.Timestamp.ToUniversalTime() : evt.Timestamp;
        sb.Append(ts.ToString("O"));

        // Level
        sb.Append(' ').Append(evt.LevelString);

        // Meta
        bool opened = false;
        void Open()
        {
            if (!opened) { sb.Append(" ["); opened = true; }
        }
        void Close()
        {
            if (opened) sb.Append(']');
        }

        if (!string.IsNullOrEmpty(evt.CategoryName))
        {
            Open(); sb.Append("Cat=").Append(evt.CategoryName);
        }

        if (evt.EventId.Id != 0 || !string.IsNullOrEmpty(evt.EventId.Name))
        {
            Open(); sb.Append(' ');
            sb.Append("EventId=").Append(evt.EventId.Id);
            if (!string.IsNullOrEmpty(evt.EventId.Name))
                sb.Append('(').Append(evt.EventId.Name).Append(')');
        }

        if (!string.IsNullOrEmpty(evt.GlobalEventId))
        {
            Open(); sb.Append(' ').Append("GEID=").Append(evt.GlobalEventId);
        }

        if (!string.IsNullOrEmpty(evt.Operation))
        {
            Open(); sb.Append(' ').Append("Op=").Append(evt.Operation);
        }

        if (!string.IsNullOrEmpty(evt.CorrelationId))
        {
            Open(); sb.Append(' ').Append("Corr=").Append(evt.CorrelationId);
        }

        if (!string.IsNullOrEmpty(evt.TraceId))
        {
            Open(); sb.Append(' ').Append("Trace=").Append(evt.TraceId);
        }

        if (!string.IsNullOrEmpty(evt.SpanId))
        {
            Open(); sb.Append(' ').Append("Span=").Append(evt.SpanId);
        }

        Close();

        // State
        if (_includeState && evt.State is { Count: > 0 })
        {
            sb.Append(' ');
            for (int i = 0; i < evt.State.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                var (k, v) = evt.State[i];
                sb.Append(k).Append('=').Append(v);
            }
        }

        // Exception
        if (!string.IsNullOrEmpty(evt.Exception))
        {
            sb.Append(" ex=").Append(evt.Exception);
        }

        return sb.ToString();
    }
}
