using Skynet.Core.Logging;
using Skynet.Core.Time;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class DayRotationPolicyMoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dayrot_more_" + Guid.NewGuid().ToString("n"));
    private readonly IPathTemplateRenderer _renderer = new DefaultPathTemplateRenderer();

    public DayRotationPolicyMoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void ResolveTarget_Uses_Subfolder_And_Template()
    {
        var clock = new FrozenClock(new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc));
        var policy = new DayRotationPolicy(_renderer, clock, _root, "t", "{yyyy}/{MM}/{dd}.log", subFolder: "logs");

        var e = new MutableLogEvent
        {
            Timestamp = new DateTimeOffset(2024, 12, 31, 10, 0, 0, TimeSpan.Zero),
            Level = Microsoft.Extensions.Logging.LogLevel.Information,
            EventId = new Microsoft.Extensions.Logging.EventId(0),
            GlobalEventId = ""
        };

        var target = policy.ResolveTarget(e, e.Timestamp);
        Assert.EndsWith(Path.Combine("t", "logs", "2024", "12", "31.log"), target.FullPath, StringComparison.OrdinalIgnoreCase);
    }
}
