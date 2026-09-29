using Sts2Sim.Core.Map;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Map;

public class StandardActMapTests
{
    private static MapPointTypeCounts DefaultCounts() => new(unknownCount: 12, restCount: 7);

    [Fact]
    public void GenerateMap_HasExactly7ColumnsAnd_MapLengthPlus1Rows()
    {
        var map = new StandardActMap(new Rng(1UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        Assert.Equal(7, map.GetColumnCount());
        Assert.Equal(16, map.GetRowCount()); // numberOfRooms(15) + 1，逐字对照 StandardActMap 构造函数
    }

    [Fact]
    public void GenerateMap_StartingMapPointHasSevenOrFewerDistinctChildren()
    {
        // 7 条起始路径可能共享同一个第 1 行节点（GenerateMap 显式去重第 2 条起，其余允许碰撞）。
        var map = new StandardActMap(new Rng(2UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        Assert.InRange(map.StartingMapPoint.Children.Count, 1, 7);
    }

    [Fact]
    public void GenerateMap_EveryNonBossPointHasAtLeastOneChild()
    {
        var map = new StandardActMap(new Rng(3UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        foreach (MapPoint point in map.GetAllMapPoints())
        {
            if (point.coord.row < map.GetRowCount() - 1)
            {
                Assert.NotEmpty(point.Children);
            }
        }
    }

    [Fact]
    public void GenerateMap_NoInvalidCrossovers()
    {
        var map = new StandardActMap(new Rng(4UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        // 交叉规则：若 A 在 col X 有个 col X+1 的子节点，B 在 col X+1 就不能有个 col X 的子节点（反之亦然）。
        foreach (MapPoint a in map.GetAllMapPoints())
        {
            foreach (MapPoint aChild in a.Children)
            {
                int delta = aChild.coord.col - a.coord.col;
                if (delta == 0)
                {
                    continue;
                }

                MapPoint? b = map.GetPoint(a.coord.col + delta, a.coord.row);
                if (b is null)
                {
                    continue;
                }

                foreach (MapPoint bChild in b.Children)
                {
                    int bDelta = bChild.coord.col - b.coord.col;
                    Assert.False(bDelta == -delta && bChild.coord.row == aChild.coord.row);
                }
            }
        }
    }

    [Fact]
    public void GenerateMap_IsDeterministicForSameSeed()
    {
        var mapA = new StandardActMap(new Rng(123UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);
        var mapB = new StandardActMap(new Rng(123UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        MapPoint[] pointsA = mapA.GetAllMapPoints().OrderBy(p => p.coord.col).ThenBy(p => p.coord.row).ToArray();
        MapPoint[] pointsB = mapB.GetAllMapPoints().OrderBy(p => p.coord.col).ThenBy(p => p.coord.row).ToArray();

        Assert.Equal(pointsA.Length, pointsB.Length);
        for (int i = 0; i < pointsA.Length; i++)
        {
            Assert.Equal(pointsA[i].coord, pointsB[i].coord);
            Assert.Equal(pointsA[i].Children.Select(c => c.coord).OrderBy(c => c.col),
                pointsB[i].Children.Select(c => c.coord).OrderBy(c => c.col));
        }
    }

    [Fact]
    public void AssignPointTypes_BossAndAncientAlwaysSet()
    {
        var map = new StandardActMap(new Rng(5UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        Assert.Equal(MapPointType.Boss, map.BossMapPoint.PointType);
        Assert.Equal(MapPointType.Ancient, map.StartingMapPoint.PointType);
    }

    [Fact]
    public void AssignPointTypes_LastRowIsAllRestSite()
    {
        var map = new StandardActMap(new Rng(6UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        foreach (MapPoint point in map.GetPointsInRow(map.GetRowCount() - 1))
        {
            Assert.Equal(MapPointType.RestSite, point.PointType);
        }
    }

    [Fact]
    public void AssignPointTypes_FirstRowIsAllMonster()
    {
        var map = new StandardActMap(new Rng(8UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        foreach (MapPoint point in map.GetPointsInRow(1))
        {
            Assert.Equal(MapPointType.Monster, point.PointType);
        }
    }

    [Fact]
    public void AssignPointTypes_NoUnassignedPointsRemain()
    {
        var map = new StandardActMap(new Rng(9UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);

        Assert.DoesNotContain(map.GetAllMapPoints(), p => p.PointType == MapPointType.Unassigned);
    }

    [Fact]
    public void IsValidPointType_RestSiteRejectedInLowerRows()
    {
        var map = new StandardActMap(new Rng(10UL), numberOfRooms: 15, DefaultCounts(), enablePruning: false);
        MapPoint lowRow = map.GetPointsInRow(map.GetRowCount() - 2).First();

        Assert.False(map.IsValidPointType(MapPointType.RestSite, lowRow));
    }
}
