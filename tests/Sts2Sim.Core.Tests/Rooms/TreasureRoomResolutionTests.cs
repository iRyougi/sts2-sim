using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public sealed class TreasureRoomResolutionTests : IDisposable
{
    public TreasureRoomResolutionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Enter_PublishesActualSuccessfulResolutionAfterCommandsComplete()
    {
        RunState runState = NewRunState("treasure-resolution-success");
        Player player = runState.Players[0];
        int goldBefore = player.Gold;
        var room = new TreasureRoom(goldAmount: 20);

        await EnterAndExitAsync(room, runState);

        TreasureRoomResolution resolution = Assert.Single(room.Resolutions);
        Assert.Same(player, resolution.Player);
        Assert.Equal(player.Gold - goldBefore, resolution.GoldGained);
        Assert.Equal(20, resolution.GoldGained);
        Assert.NotNull(resolution.RelicGained);
    }

    [Fact]
    public async Task Enter_PublishesZeroAndNullResolutionWhenTreasureGenerationIsVetoed()
    {
        RunState runState = NewRunState("treasure-resolution-veto");
        Player player = runState.Players[0];
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), player);
        int goldBefore = player.Gold;
        var room = new TreasureRoom(goldAmount: 20);

        await EnterAndExitAsync(room, runState);

        TreasureRoomResolution resolution = Assert.Single(room.Resolutions);
        Assert.Same(player, resolution.Player);
        Assert.Equal(0, resolution.GoldGained);
        Assert.Null(resolution.RelicGained);
        Assert.Equal(goldBefore, player.Gold);
    }

    [Fact]
    public async Task Enter_ResetsPriorEntryResolutionsBeforePublishingTheNewEntry()
    {
        var room = new TreasureRoom(goldAmount: 20);
        RunState firstRun = NewRunState("treasure-resolution-reset-first");
        await EnterAndExitAsync(room, firstRun);
        TreasureRoomResolution first = Assert.Single(room.Resolutions);
        Assert.True(first.GoldGained > 0);

        RunState secondRun = NewRunState("treasure-resolution-reset-second");
        Player secondPlayer = secondRun.Players[0];
        await RelicCmd.Obtain(ModelDb.Relic<SilverCrucible>(), secondPlayer);

        await EnterAndExitAsync(room, secondRun);

        TreasureRoomResolution second = Assert.Single(room.Resolutions);
        Assert.Same(secondPlayer, second.Player);
        Assert.Equal(0, second.GoldGained);
        Assert.Null(second.RelicGained);
    }

    private static RunState NewRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), runState));
        return runState;
    }

    private static async Task EnterAndExitAsync(TreasureRoom room, RunState runState)
    {
        runState.PushRoom(room);
        await room.Enter(runState);
        await room.Exit(runState);
        runState.PopCurrentRoom();
    }
}
