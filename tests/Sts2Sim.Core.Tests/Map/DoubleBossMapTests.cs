using Sts2Sim.Core.Map;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Map;

/// <summary>
/// A10 DoubleBoss 的地图层：Boss 节点之后再挂一个第二 Boss 节点。
/// 逐字对照 <c>StandardActMap.cs:96</c>（<c>GetRowCount() + 1</c> 行）、
/// <c>GenerateMap</c> 的 <c>BossMapPoint.AddChildPoint(SecondBossMapPoint)</c>、
/// 以及 <c>AssignPointTypes</c> 的 <c>SecondBossMapPoint.PointType = Boss</c>。
/// </summary>
public class DoubleBossMapTests
{
    private static MapPointTypeCounts DefaultCounts() => new(unknownCount: 12, restCount: 7);

    [Fact]
    public void WithoutSecondBoss_MapIsByteIdenticalToTheSingleBossMap()
    {
        var withDefault = new StandardActMap(new Rng(77UL), numberOfRooms: 15, DefaultCounts());
        var withExplicitFalse = new StandardActMap(
            new Rng(77UL), numberOfRooms: 15, DefaultCounts(), hasSecondBoss: false);

        Assert.Null(withDefault.SecondBossMapPoint);
        Assert.Null(withExplicitFalse.SecondBossMapPoint);
        Assert.Equal(Describe(withDefault), Describe(withExplicitFalse));
    }

    [Fact]
    public void WithSecondBoss_AddsOneNodeBelowTheBossAndKeepsTheRestIdentical()
    {
        var single = new StandardActMap(new Rng(78UL), numberOfRooms: 15, DefaultCounts());
        var doubled = new StandardActMap(
            new Rng(78UL), numberOfRooms: 15, DefaultCounts(), hasSecondBoss: true);

        MapPoint second = Assert.IsType<MapPoint>(doubled.SecondBossMapPoint);
        Assert.Equal(doubled.GetColumnCount() / 2, second.coord.col);
        Assert.Equal(doubled.GetRowCount() + 1, second.coord.row);
        Assert.Equal(MapPointType.Boss, second.PointType);
        // 第二 Boss 是第一 Boss 的唯一子节点，玩家必须打完第一个才能到第二个。
        Assert.Same(second, Assert.Single(doubled.BossMapPoint.Children));
        Assert.Empty(second.Children);
        // Grid 内的布局不受影响——第二 Boss 与 Boss 一样落在 Grid 之外。
        Assert.Equal(Describe(single), Describe(doubled));
        Assert.DoesNotContain(second, doubled.GetAllMapPoints());
    }

    [Fact]
    public void GetPointAndHasPoint_ResolveTheSecondBossNode()
    {
        var map = new StandardActMap(
            new Rng(79UL), numberOfRooms: 15, DefaultCounts(), hasSecondBoss: true);
        MapCoord coord = map.SecondBossMapPoint!.coord;

        Assert.Same(map.SecondBossMapPoint, map.GetPoint(coord));
        Assert.Same(map.SecondBossMapPoint, map.GetPoint(coord.col, coord.row));
        Assert.True(map.HasPoint(coord));
    }

    /// <summary>加参数不能消耗 RNG——false 与 true 两条路必须用掉一样多的随机数。</summary>
    [Fact]
    public void SecondBossNode_ConsumesNoAdditionalRng()
    {
        var singleRng = new Rng(80UL);
        var doubleRng = new Rng(80UL);

        _ = new StandardActMap(singleRng, numberOfRooms: 15, DefaultCounts());
        _ = new StandardActMap(doubleRng, numberOfRooms: 15, DefaultCounts(), hasSecondBoss: true);

        Assert.Equal(singleRng.Counter, doubleRng.Counter);
    }

    private static string Describe(ActMap map) => string.Join(
        "|",
        map.GetAllMapPoints()
            .OrderBy(p => p.coord.row).ThenBy(p => p.coord.col)
            .Select(p => $"{p.coord.col},{p.coord.row}:{p.PointType}"));
}
