using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Map;

public class MapPostProcessingTests
{
    [Fact]
    public void CenterGrid_ShiftsLeftWhenOnlyLeftColumnsAreEmpty()
    {
        // 7 列宽，节点只占据 col 2..4（col 0/1 空，col 5/6 也空——但右边只空了2列，左边同样2列，
        // 所以构造成右侧非空来触发单侧居中：把一个节点放在 col 6。
        var grid = new MapPoint?[7, 1];
        var occupant = new MapPoint(2, 0);
        grid[2, 0] = occupant;
        grid[6, 0] = new MapPoint(6, 0);

        MapPostProcessing.CenterGrid(grid);

        // col 0/1 空、col 5 空但 col 6 非空 => 只有左侧两列为空 => 整体左移一格。
        Assert.Equal(1, occupant.coord.col);
        Assert.Same(occupant, grid[1, 0]);
        Assert.Null(grid[2, 0]);
    }

    [Fact]
    public void CenterGrid_NoOpWhenBothSidesHaveNodesOrNeitherSideIsFullyEmpty()
    {
        var grid = new MapPoint?[7, 1];
        var occupant = new MapPoint(3, 0);
        grid[3, 0] = occupant;

        MapPostProcessing.CenterGrid(grid);

        // col 0/1 空且 col 5/6 也空 => 两侧对称，flag/flag2 同时为真 => shift 恒为 0，保持不动。
        Assert.Equal(3, occupant.coord.col);
    }

    [Fact]
    public void StraightenPaths_PullsSingleChildTowardAlignedParentAndChild()
    {
        var grid = new MapPoint?[7, 3];
        var parent = new MapPoint(3, 0);
        var kink = new MapPoint(2, 1); // 父在3、子在4，kink本身在2——两侧都比它靠右，应该被拉直到3。
        var child = new MapPoint(4, 2);
        parent.AddChildPoint(kink);
        kink.AddChildPoint(child);
        grid[3, 0] = parent;
        grid[2, 1] = kink;
        grid[4, 2] = child;

        MapPostProcessing.StraightenPaths(grid);

        Assert.Equal(3, kink.coord.col);
        Assert.Same(kink, grid[3, 1]);
        Assert.Null(grid[2, 1]);
    }

    [Fact]
    public void SpreadAdjacentMapPoints_SeparatesTwoNodesThatCanMoveApart()
    {
        var grid = new MapPoint?[7, 1];
        var left = new MapPoint(3, 0);
        var right = new MapPoint(4, 0);
        grid[3, 0] = left;
        grid[4, 0] = right;

        MapPostProcessing.SpreadAdjacentMapPoints(grid);

        Assert.True(Math.Abs(left.coord.col - right.coord.col) >= 1);
    }

    [Fact]
    public void SpreadAdjacentMapPoints_RespectsParentChildAdjacencyConstraint()
    {
        // right 有个父节点固定在 col 4，所以它最多只能挪到 col 3/4/5——不能被"展开"甩得太远。
        var grid = new MapPoint?[7, 2];
        var parent = new MapPoint(4, 0);
        var left = new MapPoint(3, 1);
        var right = new MapPoint(4, 1);
        parent.AddChildPoint(right);
        grid[4, 0] = parent;
        grid[3, 1] = left;
        grid[4, 1] = right;

        MapPostProcessing.SpreadAdjacentMapPoints(grid);

        Assert.InRange(right.coord.col, 3, 5);
    }

    /// <summary>
    /// 回归测试（偏离 #52 已解决）：Plan07 仿真-真实校验用种子 QVCMPZ1BVD08 实测真实游戏第2层
    /// （row=1）四个可走节点的列号是 {0,2,4,6}。修复前 sts2-ai 因为跳过了 MapPostProcessing，
    /// 同一种子算出的列号是 {0,3,4,6}——图连接关系相同，但列号对不上。这个测试直接走生产构造路径
    /// （RunState -&gt; StandardActMap.CreateFor 的等价路径），锁定修复后的结果与真实游戏一致。
    /// </summary>
    [Fact]
    public void RunState_Seed_QVCMPZ1BVD08_Floor2ColumnsMatchRealGame()
    {
        var runState = new RunState("QVCMPZ1BVD08", new Overgrowth());

        var floor2Cols = runState.Map.GetPointsInRow(1)
            .Select(p => p.coord.col)
            .OrderBy(c => c)
            .ToList();

        Assert.Equal(new[] { 0, 2, 4, 6 }, floor2Cols);
    }
}
