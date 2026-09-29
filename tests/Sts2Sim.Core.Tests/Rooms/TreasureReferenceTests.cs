using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public sealed class TreasureReferenceTests : IDisposable
{
    public TreasureReferenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public async Task FirstTreasure_UsesArchivedSharedRelicAndPovertyGoldRange()
    {
        const string seed = "8F9CPYQ6QYEN";
        var run = new RunState(seed, ActDefinition.GetRandomList(seed), ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        int goldBefore = player.Gold;
        var room = RoomFactory.CreateRoom(run, RoomType.Treasure);
        run.PushRoom(room);
        await room.Enter(run);

        // Archive F10 offers AKABEKO. This is the first shared-bag treasure pull;
        // prior player-bag relic draws do not alter it. Gold is range-only because
        // this focused lock does not replay preceding floors' Rewards RNG draws.
        Assert.Contains(player.Relics, relic => relic is Akabeko);
        Assert.InRange(player.Gold - goldBefore, 31, 39);
    }

    public void Dispose() => ModelDb.ResetForTests();
}
