using System.Text;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class NdjsonLogEventEncoderMoreTests
{
    private static MutableLogEvent E() => new()
    {
        Timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero),
        Level = LogLevel.Information,
        EventId = new EventId(0),
        GlobalEventId = "",
        CategoryName = null,
        Operation = null,
        CorrelationId = null,
        TraceId = null,
        SpanId = null,
        State = new List<KeyValuePair<string, object?>>()
    };

    [Fact]
    public void Encodes_Escapes_Specials_And_Exception_Newlines()
    {
        var enc = new NdjsonLogEventEncoder();
        var e = E();
        e.State = new List<KeyValuePair<string, object?>>
        {
            new("msg", "hello \"json\"\nnew"),
            new("path", @"C:\temp\file.txt"),
            new("num", 123),
        };
        e.Exception = "System.Exception: boom\r\nat X()\ninner";

        var json = Encoding.UTF8.GetString(enc.Encode(e).Span);

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2024-12-31T23:59:58.0000000Z", root.GetProperty("ts").GetString());

        // Exception: JSON enthält escaped \r\n, aber der decodierte String entspricht dem Original
        Assert.True(root.TryGetProperty("ex", out var exEl));
        var exVal = exEl.GetString();
        Assert.Equal(e.Exception, exVal);

        // State-Felder vorhanden und korrekt
        Assert.True(root.TryGetProperty("state", out var state));
        Assert.Equal("hello \"json\"\nnew", state.GetProperty("msg").GetString());
        Assert.Equal(@"C:\temp\file.txt", state.GetProperty("path").GetString());

        // Roh-JSON enthält escaped Newlines
        Assert.Contains("\\n", json);
    }

    [Fact]
    public void Omits_Empty_Optional_Fields()
    {
        var enc = new NdjsonLogEventEncoder();
        var e = E(); // viele Null/Leer
        var json = Encoding.UTF8.GetString(enc.Encode(e).Span);

        Assert.DoesNotContain("\"category\":", json);
        Assert.DoesNotContain("\"op\":", json);
        Assert.DoesNotContain("\"globalId\":", json);
        Assert.DoesNotContain("\"corr\":", json);
        Assert.DoesNotContain("\"trace\":", json);
        Assert.DoesNotContain("\"span\":", json);
        Assert.DoesNotContain("\"state\":{}", json);
    }

    [Fact]
    public void Large_State_Encodes_And_Decodes()
    {
        var enc = new NdjsonLogEventEncoder();
        var e = E();
        var list = new List<KeyValuePair<string, object?>>(1000);
        for (int i = 0; i < 1000; i++) list.Add(new KeyValuePair<string, object?>("k" + i, i));
        e.State = list;

        var bytes = enc.Encode(e);
        var s = Encoding.UTF8.GetString(bytes.Span);

        Assert.True(s.Length > 1000);
        Assert.True(enc.TryDecode(Encoding.UTF8.GetBytes(s), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(1000, parsed!.State.Count);
    }

    [Fact]
    public void Decode_Tolerates_Missing_Fields()
    {
        var enc = new NdjsonLogEventEncoder();
        var s = """{"ts":"2024-12-31T23:59:58.0000000Z","level":"Information","levelNum":2}""";
        Assert.True(enc.TryDecode(Encoding.UTF8.GetBytes(s), out var evt));
        Assert.NotNull(evt);
        Assert.Equal(LogLevel.Information, evt!.Level);
        Assert.Equal(0, evt.EventId.Id);
        Assert.Null(evt.EventId.Name);
        Assert.Empty(evt.GlobalEventId);
    }
}
