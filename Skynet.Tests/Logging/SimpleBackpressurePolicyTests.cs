using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public class SimpleBackpressurePolicyTests
{
    [Fact]
    public void None_WhenLoadBelowLower()
    {
        var p = new SimpleBackpressurePolicy(0.7, 0.9);
        var mode = p.Decide(queueLength: 6, capacity: 10); // 0.6 < 0.7
        Assert.Equal(DropMode.None, mode);
    }

    [Fact]
    public void DropNewest_WhenBetweenLowerAndUpper()
    {
        var p = new SimpleBackpressurePolicy(0.7, 0.9);
        var mode = p.Decide(queueLength: 7, capacity: 10); // 0.7 .. 0.9
        Assert.Equal(DropMode.DropNewest, mode);
    }

    [Fact]
    public void DropOldest_WhenAtOrAboveUpper()
    {
        var p = new SimpleBackpressurePolicy(0.7, 0.9);
        var mode = p.Decide(queueLength: 9, capacity: 10); // >= 0.9
        Assert.Equal(DropMode.DropOldest, mode);
    }

    [Fact]
    public void Defensive_WhenCapacityInvalid()
    {
        var p = new SimpleBackpressurePolicy();
        Assert.Equal(DropMode.DropNewest, p.Decide(queueLength: 1, capacity: 0));
        Assert.Equal(DropMode.DropNewest, p.Decide(queueLength: 1, capacity: -1));
    }

    [Fact]
    public void None_WhenEmpty()
    {
        var p = new SimpleBackpressurePolicy();
        Assert.Equal(DropMode.None, p.Decide(queueLength: 0, capacity: 10));
    }

    [Fact]
    public void Ctor_ValidatesThresholds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimpleBackpressurePolicy(-0.1, 0.9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimpleBackpressurePolicy(0.1, 1.1));
        Assert.Throws<ArgumentException>(() => new SimpleBackpressurePolicy(0.9, 0.9));
        Assert.Throws<ArgumentException>(() => new SimpleBackpressurePolicy(0.95, 0.9));
    }
}
