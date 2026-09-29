using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public class MapTravelTests : IDisposable
{
    public MapTravelTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void GetTravelablePointsFrom_ReturnsChildrenWhenFreeTravelNotAllowed()
    {
        var runState = new RunState("seed-g", new Overgrowth());
        MapPoint start = runState.Map.StartingMapPoint;

        IEnumerable<MapPoint> travelable = MapTravel.GetTravelablePointsFrom(runState, start);

        Assert.Equal(start.Children.OrderBy(p => p.coord.col), travelable.OrderBy(p => p.coord.col));
    }

    /// <summary>偏离 #320 的回归锁：Boss 点坐标在 Grid 之外，最后一行请求"下一行"必然为空。
    /// 此前自由旅行会因此返回空集合，被 RunEngine/RunDriver 判为 exhaustedMap——
    /// 满血且明明连着 Boss 却输掉整局。</summary>
    [Fact]
    public async Task GetTravelablePointsFrom_FreeTravelOnLastRow_StillReachesTheBoss()
    {
        var runState = new RunState("winged-boots-last-row", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        MapPoint lastRowPoint = runState.Map
            .GetPointsInRow(runState.Map.GetRowCount() - 1)
            .First();
        Assert.Contains(runState.Map.BossMapPoint, lastRowPoint.Children);

        // 未持有 WingedBoots 时走 Children，能取到 Boss。
        Assert.Contains(
            runState.Map.BossMapPoint,
            MapTravel.GetTravelablePointsFrom(runState, lastRowPoint));

        await RelicCmd.Obtain(ModelDb.Relic<WingedBoots>(), player);
        Assert.True(Hook.ShouldAllowFreeTravel(runState));
        // Boss 在 Grid 外，所以自由旅行查下一行必然为空——这一点与权威一致。
        Assert.Empty(runState.Map.GetPointsInRow(runState.Map.GetRowCount()));

        // 关键断言：自由旅行取不到点时回退到 Children，仍然能走到 Boss。
        Assert.Contains(
            runState.Map.BossMapPoint,
            MapTravel.GetTravelablePointsFrom(runState, lastRowPoint));
    }
}
