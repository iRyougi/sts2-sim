using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Map;

/// <summary>Map travel helpers ported from the game map traversal behavior.</summary>
public static class MapTravel
{
    /// <summary>偏离 #320（2026-09-09）：自由旅行（<c>WingedBoots</c> / Flight）在权威里是
    /// 本仓库逐字移植了向下一行取点的调用；上游取点方法的边界检查也一致，两处都没有问题。
    ///
    /// 问题在调用边界：<c>BossMapPoint</c> 的坐标位于网格中间，行号等于网格总行数，
    /// **落在 Grid 之外**，最后一行通过父子节点关系连接到 Boss。
    /// 因此在最后一行请求下一行时，权威与本仓库都会返回空集合。
    /// 权威无妨——真实游戏由 <c>NMapScreen</c> 用 <c>Children</c> 画出通往 Boss 的连线，
    /// 自由旅行并不改变那一步；但本仓库的无头驱动只有这一个取点入口，
    /// <c>RunEngine</c>/<c>RunDriver</c> 拿到空集合就判定 <c>exhaustedMap</c> 并结束这一局。
    ///
    /// 后果：带 <c>WingedBoots</c> 走到最后一行时，明明连着 Boss 且满血，
    /// 却得到 <c>Won=false</c> / <c>ReachedBoss=false</c>。
    /// 故此处在自由旅行取不到点时回退到 <see cref="MapPoint.Children"/>——
    /// 自由旅行仍只在网格内生效，跨出网格的那一步走正常连接。</summary>
    public static IEnumerable<MapPoint> GetTravelablePointsFrom(RunState runState, MapPoint currentPoint)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(currentPoint);

        if (Hook.ShouldAllowFreeTravel(runState))
        {
            IReadOnlyList<MapPoint> nextRow = runState.Map
                .GetPointsInRow(currentPoint.coord.row + 1)
                .ToList();
            if (nextRow.Count > 0)
            {
                return nextRow;
            }
        }

        return currentPoint.Children;
    }
}
