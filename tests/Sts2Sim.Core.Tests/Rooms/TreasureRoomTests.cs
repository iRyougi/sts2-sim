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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnterInternal_RelicChoice_IsAskedAfterGold_AndSkipKeepsGoldWithoutRelicOrExtraRng(bool skip)
    {
        // Baseline: no decision source (the relic is taken, as before #1073).
        (RunState baseState, Player basePlayer, _) = CreateChestRun();
        await new TreasureRoom(goldAmount: 50).EnterInternal(baseState);

        (RunState runState, Player player, int goldBefore) = CreateChestRun();
        int relicsBefore = player.Relics.Count, goldAtAsk = -1;
        IReadOnlyList<RelicModel>? offered = null;
        var room = new TreasureRoom(goldAmount: 50);
        room.ConfigureRelicChoice((p, relics) =>
        {
            offered = relics;
            goldAtAsk = p.Gold;
            return Task.FromResult<int?>(skip ? null : 0);
        });
        await room.EnterInternal(runState);

        Assert.IsType<Akabeko>(Assert.Single(offered!));
        Assert.Equal(goldBefore + 50, goldAtAsk);
        Assert.Equal(goldBefore + 50, player.Gold);
        Assert.Equal(relicsBefore + (skip ? 0 : 1), player.Relics.Count);
        Assert.Equal(skip ? null : offered![0].Id.ToString(), Assert.Single(room.Resolutions).RelicGained);
        // A skipped relic is not returned to the shared bag (the only uncommon stays pulled).
        Assert.False(runState.SharedRelicGrabBag!.HasAvailableRelics(Sts2Sim.Core.Entities.Relics.RelicRarity.Uncommon));
        Assert.Equal(RngCounters(baseState, basePlayer), RngCounters(runState, player));
    }

    private static (RunState RunState, Player Player, int GoldBefore) CreateChestRun()
    {
        var runState = new RunState("8F9CPYQ6QYEN", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player, player.Gold);
    }

    private static string RngCounters(RunState runState, Player player) => string.Join(",",
        Enum.GetValues<Sts2Sim.Core.Entities.Rngs.RunRngType>().Select(t => $"{t}={runState.Rng.GetRng(t).Counter}")
            .Concat(Enum.GetValues<Sts2Sim.Core.Entities.Rngs.PlayerRngType>()
                .Select(t => $"{t}={player.PlayerRng.GetRng(t).Counter}")));

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
