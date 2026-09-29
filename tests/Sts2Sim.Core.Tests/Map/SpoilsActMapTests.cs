using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Map;

/// <summary>
/// <see cref="SpoilsActMap"/> 是从零重写的沙漏形地图生成器（不是 <see cref="StandardActMap"/> 的后处理），
/// 它把邻接约束写成了 <see cref="InvalidOperationException"/> 守卫——违反不变量的表现是 run 中途崩溃，
/// 而不是生成一张形状不对的地图。08b-5 要跑几千局，所以这里用多种子扫描把这类罕见失败提前暴露出来。
/// <para>
/// 类注释里那句 "Tests in SpoilsActMapTest verify the core map invariants hold" 是随真实源码一起搬过来的；
/// 本文件就是它在本仓库里对应的那份测试。
/// </para>
/// <para>
/// 结构不变量合并在一个扫描用例里：每个种子建一次 <see cref="RunState"/> 就把所有检查跑完，
/// 而不是每条不变量各扫一轮——建 run 比建地图贵得多。断言消息带种子和坐标，定位不受影响。
/// </para>
/// </summary>
[Collection("ModelDb")]
public sealed class SpoilsActMapTests : IDisposable
{
    private const int SeedSweepCount = 100;

    public SpoilsActMapTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Generate_AcrossSeeds_HoldsEveryStructuralInvariant()
    {
        for (int seed = 0; seed < SeedSweepCount; seed++)
        {
            string because = $"seed spoils-{seed}";
            SpoilsActMap map = CreateMap($"spoils-{seed}");
            MapPoint treasure = AssertHourglassWaist(map, because);

            AssertEdgesAreWellFormed(map, treasure, because);
            AssertEveryPointLiesOnAStartToBossPath(map, because);
            AssertNoCrossingEdges(map, because);
            AssertEveryPointTypeIsAssigned(map, because);
            AssertStartFansOutIntoRowOne(map, because);
        }
    }

    /// <summary>沙漏的定义性质：宝箱那一行只有一个点，且所有路径都必须穿过它。</summary>
    private static MapPoint AssertHourglassWaist(SpoilsActMap map, string because)
    {
        // 逐字对照构造函数：_treasureRow = GetRowCount() - 7。
        MapPoint treasure = Assert.Single(map.GetPointsInRow(map.GetRowCount() - 7));
        Assert.True(treasure.PointType == MapPointType.Treasure, because);
        // 该点被钉死，后续 PruneAndRepair 不能改写它的类型。
        Assert.False(treasure.CanBeModified, because);
        Assert.Equal(map.GetColumnCount() / 2, treasure.coord.col);
        // 全图只有这一个宝箱点，SpoilsMap.ModifyGeneratedMapLate 靠 FirstOrDefault 找它。
        Assert.Same(treasure, Assert.Single(map.GetAllMapPoints(), p => p.PointType == MapPointType.Treasure));

        // 拿掉宝箱后 Boss 必须不可达——这正是"所有路径都经过宝箱"的等价说法。
        Assert.False(
            BossIsReachableWithout(map, treasure.coord),
            $"{because}: 存在绕开宝箱通往 Boss 的路径，沙漏腰部没有收紧");
        return treasure;
    }

    /// <summary>
    /// 每条边都必须正好跨 1 行，父子引用双向一致；列位移不超过 1 这条只对普通边成立。
    /// <para>
    /// 沙漏本身要求三处扇入/扇出打破严格邻接，这是设计而非缺陷：起点连向第 1 行的每个点
    /// （<c>ConnectRowToStart</c>）、最后一行的每个点连向 Boss（<c>ConnectRowToBoss</c>）、
    /// 腰部整行被折叠进中心宝箱（<c>RedirectToTreasure</c>）。豁免范围写死在这里，
    /// 任何第四处越界都会让测试失败。
    /// </para>
    /// </summary>
    private static void AssertEdgesAreWellFormed(SpoilsActMap map, MapPoint treasure, string because)
    {
        foreach (MapPoint point in AllPoints(map))
        {
            foreach (MapPoint child in point.Children)
            {
                Assert.True(
                    child.coord.row == point.coord.row + 1,
                    $"{because}: {point} -> {child} 跨了不止一行");
                Assert.Contains(point, child.parents);

                bool isHourglassJoint =
                    point == map.StartingMapPoint || child == map.BossMapPoint ||
                    point == treasure || child == treasure;
                if (isHourglassJoint)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(child.coord.col - point.coord.col) <= 1,
                    $"{because}: {point} -> {child} 列位移超过 1");
            }
        }
    }

    /// <summary>没有任何点被剪枝孤立在图外——每个点都同时"起点可达"且"能走到 Boss"。</summary>
    private static void AssertEveryPointLiesOnAStartToBossPath(SpoilsActMap map, string because)
    {
        HashSet<MapPoint> fromStart = Traverse(map.StartingMapPoint, p => p.Children);
        HashSet<MapPoint> toBoss = Traverse(map.BossMapPoint, p => p.parents);

        Assert.Contains(map.BossMapPoint, fromStart);
        foreach (MapPoint point in map.GetAllMapPoints())
        {
            Assert.True(fromStart.Contains(point), $"{because}: {point} 从起点不可达");
            Assert.True(toBoss.Contains(point), $"{because}: {point} 到不了 Boss");
        }
    }

    /// <summary>
    /// 交叉规则与 <see cref="StandardActMap"/> 同一条：A 在 col X 有个 col X+1 的子节点时，
    /// col X+1 上的 B 就不能有个 col X 的子节点。
    /// </summary>
    private static void AssertNoCrossingEdges(SpoilsActMap map, string because)
    {
        foreach (MapPoint a in AllPoints(map))
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
                    Assert.False(
                        bChild.coord.col - b.coord.col == -delta && bChild.coord.row == aChild.coord.row,
                        $"{because}: {a}->{aChild} 与 {b}->{bChild} 交叉");
                }
            }
        }
    }

    /// <summary>
    /// 固定行的类型是写死的：第 1 行全 Monster、最后一行全 RestSite、起点 Ancient、终点 Boss，
    /// 并且 <c>AssignPointTypes</c> 收尾会把剩余的 Unassigned 兜底成 Monster。
    /// </summary>
    private static void AssertEveryPointTypeIsAssigned(SpoilsActMap map, string because)
    {
        Assert.Equal(MapPointType.Ancient, map.StartingMapPoint.PointType);
        Assert.Equal(MapPointType.Boss, map.BossMapPoint.PointType);

        Assert.All(map.GetPointsInRow(1), p =>
        {
            Assert.Equal(MapPointType.Monster, p.PointType);
            Assert.False(p.CanBeModified, because);
        });
        Assert.All(map.GetPointsInRow(map.GetRowCount() - 1), p =>
        {
            Assert.Equal(MapPointType.RestSite, p.PointType);
            Assert.False(p.CanBeModified, because);
        });
        Assert.DoesNotContain(map.GetAllMapPoints(), p => p.PointType == MapPointType.Unassigned);
    }

    /// <summary>Grid 第 0 行始终为空：路径从第 1 行起生成，起点是独立于 Grid 的节点。</summary>
    private static void AssertStartFansOutIntoRowOne(SpoilsActMap map, string because)
    {
        Assert.Empty(map.GetPointsInRow(0));
        MapPoint[] rowOne = map.GetPointsInRow(1).ToArray();
        Assert.Equal(rowOne.ToHashSet(), map.StartingMapPoint.Children);
        // Upstream GenerateHourglassMap guarantees two starts before PruneAndRepair.
        // PruneSegment may remove one while their parent still has two children;
        // its one-child-parent guard protects the final route, not a two-route minimum.
        Assert.InRange(rowOne.Length, 1, 7);
        Assert.All(map.startMapPoints, p => Assert.Equal(1, p.coord.row));
        Assert.Equal(rowOne.ToHashSet(), map.startMapPoints);
    }

    [Fact]
    public void Generate_HasSevenColumnsAndOneRowPerRoomPlusOne()
    {
        SpoilsActMap map = CreateMap("spoils-shape");

        Assert.Equal(7, map.GetColumnCount());
        // 逐字对照构造函数：_mapLength = act.BaseNumberOfRooms + 1，Hive 是 14。
        Assert.Equal(15, map.GetRowCount());
        Assert.Equal(3, map.StartingMapPoint.coord.col);
        Assert.Equal(0, map.StartingMapPoint.coord.row);
        Assert.Equal(3, map.BossMapPoint.coord.col);
        // 起点与 Boss 都不落在 Grid 里，所以 GetAllMapPoints() 不含它们。
        Assert.Equal(15, map.BossMapPoint.coord.row);
        Assert.DoesNotContain(map.BossMapPoint, map.GetAllMapPoints());
        Assert.DoesNotContain(map.StartingMapPoint, map.GetAllMapPoints());
    }

    [Fact]
    public void Generate_IsDeterministicForTheSameSeedAndVariesAcrossSeeds()
    {
        Assert.Equal(Describe(CreateMap("spoils-determinism")), Describe(CreateMap("spoils-determinism")));

        // 不同种子必须真的产出不同地图，否则上面的多种子扫描等于只测了一张图。
        var shapes = new HashSet<string>();
        for (int seed = 0; seed < 20; seed++)
        {
            shapes.Add(Describe(CreateMap($"spoils-variety-{seed}")));
        }

        Assert.True(shapes.Count > 15, $"20 个种子只产出了 {shapes.Count} 种不同地图");
    }

    /// <summary>
    /// 地图用的是独立派生的 <c>"spoils_map"</c> 流，构造它不能动到 run 的其它 RNG 计数器——
    /// 否则同一 seed 下拿没拿到藏宝图会让后续所有抽取错位。
    /// </summary>
    [Fact]
    public void Generate_DoesNotDisturbTheRunRngCounters()
    {
        RunState run = CreateActTwoRun("spoils-rng-isolation");
        int upFront = run.Rng.UpFront.Counter;
        int unknown = run.Rng.UnknownMapPoint.Counter;

        var map = new SpoilsActMap(run);

        Assert.NotNull(map.StartingMapPoint);
        Assert.Equal(upFront, run.Rng.UpFront.Counter);
        Assert.Equal(unknown, run.Rng.UnknownMapPoint.Counter);
    }

    /// <summary>显式传入点数时，生成器必须照单使用而不是自己再摇一次。</summary>
    [Fact]
    public void Generate_HonorsAnExplicitPointTypeCountOverride()
    {
        RunState run = CreateActTwoRun("spoils-explicit-counts");

        string sparse = Describe(new SpoilsActMap(run, new MapPointTypeCounts(unknownCount: 10, restCount: 4)));
        string dense = Describe(new SpoilsActMap(run, new MapPointTypeCounts(unknownCount: 14, restCount: 7)));

        Assert.Equal(
            sparse,
            Describe(new SpoilsActMap(run, new MapPointTypeCounts(unknownCount: 10, restCount: 4))));
        Assert.NotEqual(sparse, dense);
    }

    private static SpoilsActMap CreateMap(string seed) => new(CreateActTwoRun(seed));

    /// <summary>SpoilsMap 只在第 2 幕生效，而幕定义会校验 <c>Index</c>，所以必须从第 1 幕推进过去。</summary>
    private static RunState CreateActTwoRun(string seed)
    {
        var run = new RunState(seed, [new Overgrowth(), new Hive()]);
        run.AdvanceToNextAct();
        return run;
    }

    /// <summary>Grid 里的点，加上不在 Grid 里的起点。</summary>
    private static IEnumerable<MapPoint> AllPoints(SpoilsActMap map) =>
        map.GetAllMapPoints().Append(map.StartingMapPoint);

    private static HashSet<MapPoint> Traverse(MapPoint from, Func<MapPoint, IEnumerable<MapPoint>> next)
    {
        var seen = new HashSet<MapPoint> { from };
        var pending = new Stack<MapPoint>([from]);
        while (pending.Count > 0)
        {
            foreach (MapPoint neighbour in next(pending.Pop()))
            {
                if (seen.Add(neighbour))
                {
                    pending.Push(neighbour);
                }
            }
        }
        return seen;
    }

    /// <summary>从起点出发、绕开 <paramref name="blocked"/>，看能否到达 Boss。</summary>
    private static bool BossIsReachableWithout(SpoilsActMap map, MapCoord blocked)
    {
        var seen = new HashSet<MapPoint>();
        var pending = new Stack<MapPoint>([map.StartingMapPoint]);
        while (pending.Count > 0)
        {
            MapPoint current = pending.Pop();
            if (current.coord.Equals(blocked) || !seen.Add(current))
            {
                continue;
            }
            if (current == map.BossMapPoint)
            {
                return true;
            }
            foreach (MapPoint child in current.Children)
            {
                pending.Push(child);
            }
        }
        return false;
    }

    private static string Describe(SpoilsActMap map) => string.Join(
        "|",
        AllPoints(map)
            .OrderBy(p => p.coord.row).ThenBy(p => p.coord.col)
            .Select(p => $"{p.coord.col},{p.coord.row}:{p.PointType}:" + string.Join(
                ">", p.Children.OrderBy(c => c.coord.col).Select(c => c.coord.col))));
}
