using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

public sealed class NdjsonLogEventEncoder : ILogEventEncoder
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = false
    };

    public string ContentType => "application/x-ndjson";
    public string Version => "1.0";

    public ReadOnlyMemory<byte> Encode(ILogEvent evt)
    {
        // Schreiben in IBufferWriter für minimale Allokationen
        using var buffer = new PooledBufferWriter(1024);
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();

            // Pflicht/Meta
            var tsUtc = evt.Timestamp.ToUniversalTime();
            // Explizit "Z"-Suffix erzwingen (statt +00:00, das als \u002B escaped wird)
            var tsString = tsUtc.UtcDateTime.ToString("O"); // ergibt ...Z
            writer.WriteString("ts", tsString);

            writer.WriteString("level", evt.LevelString);
            writer.WriteNumber("levelNum", (int)evt.Level);

            // EventId
            writer.WriteStartObject("eventId");
            writer.WriteNumber("id", evt.EventId.Id);
            if (!string.IsNullOrEmpty(evt.EventId.Name))
                writer.WriteString("name", evt.EventId.Name);
            writer.WriteEndObject();

            // IDs, Category, Operation
            if (!string.IsNullOrEmpty(evt.GlobalEventId)) writer.WriteString("globalId", evt.GlobalEventId);
            if (!string.IsNullOrEmpty(evt.CategoryName)) writer.WriteString("category", evt.CategoryName);
            if (!string.IsNullOrEmpty(evt.Operation)) writer.WriteString("op", evt.Operation);

            // Correlation/Trace
            if (!string.IsNullOrEmpty(evt.CorrelationId)) writer.WriteString("corr", evt.CorrelationId);
            if (!string.IsNullOrEmpty(evt.TraceId)) writer.WriteString("trace", evt.TraceId);
            if (!string.IsNullOrEmpty(evt.SpanId)) writer.WriteString("span", evt.SpanId);

            // Exception
            if (!string.IsNullOrEmpty(evt.Exception)) writer.WriteString("ex", evt.Exception);

            // State als Objekt
            if (evt.State is { Count: > 0 })
            {
                writer.WriteStartObject("state");
                for (int i = 0; i < evt.State.Count; i++)
                {
                    var (k, v) = evt.State[i];
                    WriteValue(writer, k, v);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();
        }

        return buffer.WrittenMemory;
    }

    public bool TryDecode(ReadOnlySpan<byte> payload, out ILogEvent? evt)
    {
        try
        {
            using var doc = JsonDocument.Parse(new ReadOnlyMemory<byte>(payload.ToArray()));
            var root = doc.RootElement;

            var ts = root.GetPropertyOrDefault("ts")?.GetString();
            var levelNum = root.GetPropertyOrDefault("levelNum")?.GetInt32() ?? (int)LogLevel.Information;
            var level = (LogLevel)levelNum;

            var evId = root.GetPropertyOrDefault("eventId");
            var eidId = evId?.GetPropertyOrDefault("id")?.GetInt32() ?? 0;
            var eidName = evId?.GetPropertyOrDefault("name")?.GetString();

            var e = new MutableLogEvent
            {
                Timestamp = ParseDateTimeOffset(ts) ?? DateTimeOffset.UtcNow,
                Level = level,
                EventId = new EventId(eidId, eidName),
                GlobalEventId = root.GetPropertyOrDefault("globalId")?.GetString() ?? "",
                CategoryName = root.GetPropertyOrDefault("category")?.GetString(),
                Operation = root.GetPropertyOrDefault("op")?.GetString(),
                Exception = root.GetPropertyOrDefault("ex")?.GetString(),
                CorrelationId = root.GetPropertyOrDefault("corr")?.GetString(),
                TraceId = root.GetPropertyOrDefault("trace")?.GetString(),
                SpanId = root.GetPropertyOrDefault("span")?.GetString(),
                State = ReadState(root.GetPropertyOrDefault("state"))
            };

            evt = e;
            return true;
        }
        catch
        {
            evt = null;
            return false;
        }
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
            return dto;
        return null;
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> ReadState(JsonElement? stateEl)
    {
        if (stateEl is null || stateEl.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<KeyValuePair<string, object?>>();

        var obj = stateEl.Value;
        var list = new List<KeyValuePair<string, object?>>(obj.GetRawText().Length / 12);

        foreach (var p in obj.EnumerateObject())
        {
            list.Add(new KeyValuePair<string, object?>(p.Name, ReadJsonValue(p.Value)));
        }
        return list;
    }

    private static object? ReadJsonValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.TryGetDouble(out var d) ? d : (object?)el.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => el.GetRawText(),   // Roh-JSON, um komplexe Werte zu tragen
        JsonValueKind.Array => el.GetRawText(),
        _ => el.GetRawText()
    };

    private static void WriteValue(Utf8JsonWriter w, string key, object? value)
    {
        if (value is null) { w.WriteNull(key); return; }
        switch (value)
        {
            case string s: w.WriteString(key, s); break;
            case int i: w.WriteNumber(key, i); break;
            case long l: w.WriteNumber(key, l); break;
            case double d: w.WriteNumber(key, d); break;
            case float f: w.WriteNumber(key, f); break;
            case bool b: w.WriteBoolean(key, b); break;
            default:
                // Fallback: ToString
                w.WriteString(key, value.ToString());
                break;
        }
    }

    // Kleiner gepoolter BufferWriter für weniger Allokationen
    private sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer;
        private int _written;
        public byte[] ToArray()
        {
            var result = new byte[_written];
            Buffer.BlockCopy(_buffer, 0, result, 0, _written);
            return result;
        }

        public PooledBufferWriter(int initialSize) => _buffer = ArrayPool<byte>.Shared.Rent(initialSize);

        public void Advance(int count) => _written += count;
        public Memory<byte> GetMemory(int sizeHint = 0) => Ensure(sizeHint);
        public Span<byte> GetSpan(int sizeHint = 0) => Ensure(sizeHint).Span;

        private Memory<byte> Ensure(int sizeHint)
        {
            if (sizeHint <= 0) sizeHint = 256;
            if (_buffer.Length - _written < sizeHint)
            {
                var newBuf = ArrayPool<byte>.Shared.Rent(Math.Max(_buffer.Length * 2, _written + sizeHint));
                Buffer.BlockCopy(_buffer, 0, newBuf, 0, _written);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = newBuf;
            }
            return new Memory<byte>(_buffer, _written, _buffer.Length - _written);
        }

        public ReadOnlyMemory<byte> WrittenMemory => new ReadOnlyMemory<byte>(_buffer, 0, _written);

        // NdjsonLogEventEncoder.Encode(...)
        public ReadOnlyMemory<byte> Encode(ILogEvent evt)
        {
            using var buffer = new PooledBufferWriter(1024);
            using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.Flush();
            }

            // WICHTIG: gepoolten Speicher nicht direkt herausgeben
            return buffer.ToArray(); // <-- sicheres, eigenes Array zurückgeben
        }
        
        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = [];
            _written = 0;
        }
    }
}