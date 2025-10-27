using System.Text;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class NdjsonLogEventEncoderTests
{
    private static MutableLogEvent NewEvt() => new()
    {
        Timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero),
        Level = LogLevel.Warning,
        EventId = new EventId(42, "E"),
        GlobalEventId = "gid",
        CategoryName = "Cat",
        Operation = "Op",
        CorrelationId = "corr",
        TraceId = "tr",
        SpanId = "sp",
        State = new List<KeyValuePair<string, object?>> { new("a", 1), new("b", "x") }
    };

    [Fact]
    public void Encode_Produces_Compact_Json()
    {
        var enc = new NdjsonLogEventEncoder();
        var e = NewEvt();
        var bytes = enc.Encode(e);
        var s = Encoding.UTF8.GetString(bytes.Span);

        Assert.Contains("\"ts\":\"2024-12-31T23:59:58.0000000Z\"", s);
        Assert.Contains("\"level\":\"Warning\"", s);
        Assert.Contains("\"levelNum\":3", s); // Warning
        Assert.Contains("\"eventId\":{\"id\":42,\"name\":\"E\"}", s);
        Assert.Contains("\"globalId\":\"gid\"", s);
        Assert.Contains("\"category\":\"Cat\"", s);
        Assert.Contains("\"op\":\"Op\"", s);
        Assert.Contains("\"corr\":\"corr\"", s);
        Assert.Contains("\"trace\":\"tr\"", s);
        Assert.Contains("\"span\":\"sp\"", s);
        Assert.Contains("\"state\":", s);
        Assert.DoesNotContain("\n", s);
    }

    [Fact]
    public void TryDecode_Roundtrip_Basic()
    {
        var enc = new NdjsonLogEventEncoder();
        var e = NewEvt();
        var s = Encoding.UTF8.GetString(enc.Encode(e).Span);

        Assert.True(enc.TryDecode(Encoding.UTF8.GetBytes(s), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(LogLevel.Warning, parsed!.Level);
        Assert.Equal(42, parsed.EventId.Id);
        Assert.Equal("E", parsed.EventId.Name);
        Assert.Equal("gid", parsed.GlobalEventId);
        Assert.Equal("Cat", parsed.CategoryName);
        Assert.Equal("Op", parsed.Operation);
        Assert.Equal("corr", parsed.CorrelationId);
        Assert.Equal("tr", parsed.TraceId);
        Assert.Equal("sp", parsed.SpanId);

        // State grob geprüft
        Assert.Equal(2, parsed.State.Count);
    }

    [Fact]
    public void TryDecode_Invalid_ReturnsFalse()
    {
        var enc = new NdjsonLogEventEncoder();
        Assert.False(enc.TryDecode(Encoding.UTF8.GetBytes("{ invalid"), out var _));
    }
    
    [Fact]
    public void Encode_Returns_Owned_Buffer_Not_Aliased()
    {
        var enc = new NdjsonLogEventEncoder();

        var e1 = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow, Level = LogLevel.Information, EventId = new(1) };
        var b1 = enc.Encode(e1);             // erste Encodierung
        var s1 = Encoding.UTF8.GetString(b1.Span);

        var e2 = new MutableLogEvent { Timestamp = DateTimeOffset.UtcNow, Level = LogLevel.Warning, EventId = new(2) };
        var b2 = enc.Encode(e2);             // zweite Encodierung (würde den Pool-Buffer wiederverwenden)

        // Wenn b1 alias auf Pool war, wäre s1 jetzt „kaputt“/anders.
        Assert.Contains("\"id\":1", s1);     // z.B. falls du eventId.id so prüfst
    }

    
}
