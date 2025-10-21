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

        // Versuche, mutable Events direkt zu setzen (häufigster Fall)
        if (evt is MutableLogEvent m)
        {
            EnrichMutable(m);
            return;
        }

        // Fallback (immutable): nichts werfen, aber versuchen, via dynamic zu setzen
        EnrichDynamic(evt);
    }

    private static void EnrichMutable(MutableLogEvent e)
    {
        // GlobalEventId
        if (string.IsNullOrWhiteSpace(e.GlobalEventId))
        {
            e.GlobalEventId = Guid.NewGuid().ToString("n");
        }

        var activity = Activity.Current;

        if (activity is not null)
        {
            // Trace/Span
            if (string.IsNullOrEmpty(e.TraceId))
                e.TraceId = activity.TraceId.ToString();
            if (string.IsNullOrEmpty(e.SpanId))
                e.SpanId = activity.SpanId.ToString();

            // CorrelationId: vorhandenes behalten, sonst Activity.TraceId
            if (string.IsNullOrEmpty(e.CorrelationId))
                e.CorrelationId = activity.TraceId.ToString();
        }
        else
        {
            // Kein Activity-Kontext -> CorrelationId generieren, falls leer
            if (string.IsNullOrEmpty(e.CorrelationId))
                e.CorrelationId = Guid.NewGuid().ToString("n");
        }
    }

    private static void EnrichDynamic(ILogEvent evt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evt.GlobalEventId))
            {
                SetIfWritable(ref evt, static (e, v) => e.GlobalEventId = v, Guid.NewGuid().ToString("n"));
            }

            var activity = Activity.Current;
            if (activity is not null)
            {
                if (string.IsNullOrEmpty(evt.TraceId))
                    SetIfWritable(ref evt, static (e, v) => e.TraceId = v, activity.TraceId.ToString());
                if (string.IsNullOrEmpty(evt.SpanId))
                    SetIfWritable(ref evt, static (e, v) => e.SpanId = v, activity.SpanId.ToString());
                if (string.IsNullOrEmpty(evt.CorrelationId))
                    SetIfWritable(ref evt, static (e, v) => e.CorrelationId = v, activity.TraceId.ToString());
            }
            else
            {
                if (string.IsNullOrEmpty(evt.CorrelationId))
                    SetIfWritable(ref evt, static (e, v) => e.CorrelationId = v, Guid.NewGuid().ToString("n"));
            }
        }
        catch
        {
            // niemals werfen
        }
    }

    private static void SetIfWritable<T>(ref ILogEvent evt, Action<dynamic, T> setter, T value)
    {
        try { setter((dynamic)evt, value); } catch { /* ignore */ }
    }
}
