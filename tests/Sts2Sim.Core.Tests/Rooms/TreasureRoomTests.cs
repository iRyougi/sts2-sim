using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public class TreasureRoomTests : IDisposable
{
    public TreasureRoomTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight), typeof(Vajra), typeof(Akabeko), typeof(Circlet),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EnterInternal_GrantsGoldToEveryPlayer()
    {
        var runState = new RunState("treasure-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        int goldBefore = player.Gold;

        var room = new TreasureRoom(goldAmount: 50);
        await room.EnterInternal(runState);

        Assert.Equal(goldBefore + 50, player.Gold);
    }

    [Fact]
    public async Task EnterInternal_GrantsSharedUncommonRelic_WhenRarityRollIsUncommon()
    {
        var runState = new RunState("8F9CPYQ6QYEN", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var room = new TreasureRoom(goldAmount: 0);
        await room.EnterInternal(runState);

        Assert.Contains(player.Relics, relic => relic is Akabeko);
    }

    [Fact]
    public async Task EnterInternal_WhenSharedPoolIsExhausted_GrantsCirclet()
    {
        var runState = new RunState("treasure-exhausted", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        Assert.IsType<Vajra>(runState.SharedRelicGrabBag!.PullFromFront(Sts2Sim.Core.Entities.Relics.RelicRarity.Common));
        Assert.IsType<Akabeko>(runState.SharedRelicGrabBag.PullFromFront(Sts2Sim.Core.Entities.Relics.RelicRarity.Uncommon));
        Assert.True(player.RelicGrabBag.HasAvailableRelics(Sts2Sim.Core.Entities.Relics.RelicRarity.Common));

        var room = new TreasureRoom(goldAmount: 0);
        await room.EnterInternal(runState);

        Assert.Contains(player.Relics, relic => relic is Circlet);
    }
}
