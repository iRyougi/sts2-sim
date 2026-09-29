using Sts2Sim.Core.Map;

namespace Sts2Sim.Core.Tests.Map;

public class MapPointTests
{
    [Fact]
    public void AddChildPoint_LinksBothDirections()
    {
        var parent = new MapPoint(3, 0);
        var child = new MapPoint(3, 1);

        parent.AddChildPoint(child);

        Assert.Contains(child, parent.Children);
        Assert.Contains(parent, child.parents);
    }

    [Fact]
    public void RemoveChildPoint_UnlinksBothDirections()
    {
        var parent = new MapPoint(3, 0);
        var child = new MapPoint(3, 1);
        parent.AddChildPoint(child);

        parent.RemoveChildPoint(child);

        Assert.DoesNotContain(child, parent.Children);
        Assert.DoesNotContain(parent, child.parents);
    }

    [Fact]
    public void CompareTo_OrdersByColThenRow()
    {
        var a = new MapPoint(0, 0);
        var b = new MapPoint(1, 0);

        Assert.True(a.CompareTo(b) < 0);
    }
}
