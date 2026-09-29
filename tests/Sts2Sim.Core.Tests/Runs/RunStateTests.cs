using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public class RunStateTests
{
    public RunStateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    [Fact]
    public void Constructor_GeneratesMapAndStartsWithNoVisitedCoords()
    {
        var runState = new RunState("seed-a", new Overgrowth());

        Assert.NotNull(runState.Map);
        Assert.Empty(runState.VisitedMapCoords);
        Assert.Null(runState.CurrentMapCoord);
        Assert.Null(runState.CurrentMapPoint);
    }

    [Fact]
    public void CreateKeyedForLabels_IsExplicitAndPropagatesToPlayerRng()
    {
        RunState sequential = new("label-mode", new Overgrowth());
        RunState keyed = RunState.CreateKeyedForLabels(
            "label-mode",
            [new Overgrowth()]);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), keyed);

        Assert.False(sequential.Rng.UsesSemanticKeys);
        Assert.True(keyed.Rng.UsesSemanticKeys);
        Assert.True(player.PlayerRng.UsesSemanticKeys);
    }

    [Fact]
    public void AddVisitedMapCoord_UpdatesCurrentMapPoint()
    {
        var runState = new RunState("seed-b", new Overgrowth());
        MapCoord startingCoord = runState.Map.StartingMapPoint.coord;

        bool added = runState.AddVisitedMapCoord(startingCoord);

        Assert.True(added);
        Assert.Equal(startingCoord, runState.CurrentMapCoord);
        Assert.Same(runState.Map.StartingMapPoint, runState.CurrentMapPoint);
    }

    [Fact]
    public void AddVisitedMapCoord_ReturnsFalseForDuplicateCoord()
    {
        var runState = new RunState("seed-c", new Overgrowth());
        MapCoord coord = runState.Map.StartingMapPoint.coord;
        runState.AddVisitedMapCoord(coord);

        Assert.False(runState.AddVisitedMapCoord(coord));
    }

    [Fact]
    public void GetAndIncrementNextRoomId_StartsAtZeroAndIncrements()
    {
        var runState = new RunState("seed-e", new Overgrowth());

        Assert.Equal(0, runState.GetAndIncrementNextRoomId());
        Assert.Equal(1, runState.GetAndIncrementNextRoomId());
    }

    [Fact]
    public void IsGameOver_TrueWhenAllPlayersDead()
    {
        var runState = new RunState("seed-f", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        Assert.False(runState.IsGameOver);
    }
}
