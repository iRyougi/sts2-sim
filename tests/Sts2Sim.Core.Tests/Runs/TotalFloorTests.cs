using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

/// <summary>
/// 真实游戏的 TotalFloor 是 MapPointHistory 跨幕求和。本仓库的
/// VisitedMapCoords 在 AdvanceToNextAct 里被清空，只能表达"本幕房间数"。
/// 偏离 #108 的门控按 TotalFloor 判定，所以必须有一个真正跨幕累积的计数。
/// </summary>
[Collection("ModelDb")]
public sealed class TotalFloorTests : IDisposable
{
    public TotalFloorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void TotalFloor_CountsVisitedRooms_WithinOneAct()
    {
        RunState runState = CreateTwoActRun("total-floor-single");

        Assert.Equal(0, runState.TotalFloor);
        runState.AddVisitedMapCoord(new MapCoord(0, 0));
        runState.AddVisitedMapCoord(new MapCoord(1, 0));

        Assert.Equal(2, runState.TotalFloor);
    }

    [Fact]
    public void TotalFloor_KeepsAccumulating_AcrossActAdvance()
    {
        RunState runState = CreateTwoActRun("total-floor-across");
        runState.AddVisitedMapCoord(new MapCoord(0, 0));
        runState.AddVisitedMapCoord(new MapCoord(1, 0));

        runState.AdvanceToNextAct();

        // 本幕计数归零，但跨幕总数必须保留。
        Assert.Empty(runState.VisitedMapCoords);
        Assert.Equal(2, runState.TotalFloor);

        runState.AddVisitedMapCoord(new MapCoord(0, 0));
        Assert.Equal(3, runState.TotalFloor);
    }

    [Fact]
    public void CombatClone_CapturesTotalFloorAtCloneTime()
    {
        var runState = new RunState("total-floor-clone", new Overgrowth());
        runState.AddVisitedMapCoord(new MapCoord(0, 0));
        CombatState clone = new CombatState(runState).Clone();

        runState.AddVisitedMapCoord(new MapCoord(1, 0));

        Assert.Equal(1, clone.RunState.TotalFloor);
        Assert.Equal(2, runState.TotalFloor);
    }

    private static RunState CreateTwoActRun(string seed)
    {
        var runState = new RunState(seed, new ActDefinition[] { new Overgrowth(), new Hive() });
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        return runState;
    }
}
