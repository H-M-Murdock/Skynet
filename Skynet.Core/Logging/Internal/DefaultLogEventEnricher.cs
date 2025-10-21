using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

/// <summary>
/// Standard-Enricher:
/// - Setzt GlobalEventId (GuidV7-ähnlich, fallback Guid.NewGuid).
/// - Übernimmt Trace/Span aus Activity.Current.
/// - Setzt CorrelationId gemäß Regel: vorhandenes behalten; sonst Activity.TraceId; sonst neue Guid.
/// - Belässt bestehende Felder unverändert; leichte, synchrone Operationen.
/// </summary>
public sealed class DefaultEnricher : IEnricher
{
    public void Enrich(ILogEvent evt)
    {
        if (evt is null) return;

        // GlobalEventId
        if (string.IsNullOrWhiteSpace(evt.GlobalEventId))
        {
            // GuidV7 wäre ideal; bis verfügbar: Guid.NewGuid in "n"
            var id = Guid.NewGuid().ToString("n");
            SetIfWritable(ref evt, static (e, v) => e.GlobalEventId = v, id);
        }

        var activity = Activity.Current;

        // TraceId/SpanId
        if (activity is not null)
        {
            if (string.IsNullOrEmpty(evt.TraceId))
                SetIfWritable(ref evt, static (e, v) => e.TraceId = v, activity.TraceId.ToString());
            if (string.IsNullOrEmpty(evt.SpanId))
                SetIfWritable(ref evt, static (e, v) => e.SpanId = v, activity.SpanId.ToString());

            // CorrelationId Priorität: bestehend -> Activity.TraceId -> neu
            if (string.IsNullOrEmpty(evt.CorrelationId))
                SetIfWritable(ref evt, static (e, v) => e.CorrelationId = v, activity.TraceId.ToString());
        }
        else
        {
            if (string.IsNullOrEmpty(evt.CorrelationId))
                SetIfWritable(ref evt, static (e, v) => e.CorrelationId = v, Guid.NewGuid().ToString("n"));
        }

        // LevelString ist computed; Level bleibt wie ist.
        // EventId (MEL) bleibt wie gesetzt; bei Bedarf könnte man einen Default (0) setzen – bereits Standard.
    }

    // Da ILogEvent nur Getter hat, erlauben wir Enrichment für mutable Implementierungen,
    // indem wir auf bekannte, interne Typen mit Settern setzen. Sonst: no-op.
    private static void SetIfWritable<T>(ref ILogEvent evt, Action<dynamic, T> setter, T value)
    {
        try
        {
            setter((dynamic)evt, value);
        }
        catch
        {
            // Implementierung ist immutable -> ignorieren (Enricher soll nie werfen)
        }
    }
}
