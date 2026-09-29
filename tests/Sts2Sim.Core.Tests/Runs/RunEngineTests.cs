using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Runs;

public class RunEngineTests : IDisposable
{
    public RunEngineTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose()
    {
        ModelDb.ResetForTests();
    }

    private static MapPoint PickFirstByCoord(IEnumerable<MapPoint> points) =>
        points.OrderBy(p => p.coord.col).First();

    [Fact]
    public async Task RunAsync_TerminatesAndOnlyWinsIfBossWasReached()
    {
        var runState = new RunState("run-engine-a", new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var engine = new RunEngine(runState, PickFirstByCoord);
        RunEngine.Result result = await engine.RunAsync(maxFloors: 200);

        Assert.True(result.FloorsVisited > 0);
        Assert.True(result.FloorsVisited < 200);
        if (result.Won)
        {
            Assert.True(result.ReachedBoss);
        }
    }

    [Fact]
    public void RunAsync_IsDeterministicForSameSeed()
    {
        RunEngine.Result RunOnce()
        {
            var runState = new RunState("run-engine-b", new Overgrowth());
            runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            var engine = new RunEngine(runState, PickFirstByCoord);
            return engine.RunAsync(maxFloors: 200).GetAwaiter().GetResult();
        }

        RunEngine.Result resultA = RunOnce();
        RunEngine.Result resultB = RunOnce();

        Assert.Equal(resultA.Won, resultB.Won);
        Assert.Equal(resultA.FloorsVisited, resultB.FloorsVisited);
        Assert.Equal(resultA.FinalPlayerHp, resultB.FinalPlayerHp);
    }

    [Fact]
    public async Task RunAsync_ReturnsLossImmediately_WhenPlayerAlreadyDead()
    {
        var runState = new RunState("run-engine-c", new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.Creature.LoseHpInternal(player.Creature.MaxHp, default(ValueProp));
        Assert.True(runState.IsGameOver);

        var engine = new RunEngine(runState, PickFirstByCoord);
        RunEngine.Result result = await engine.RunAsync(maxFloors: 200);

        Assert.False(result.Won);
        Assert.True(runState.IsGameOver);
    }
}
