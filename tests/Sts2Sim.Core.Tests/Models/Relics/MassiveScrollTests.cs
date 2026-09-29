using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class MassiveScrollTests : IDisposable
{
    public MassiveScrollTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task SinglePlayerGateIsFalseAndForcedObtainIsSafeNoOp()
    {
        (RunState runState, Player player) = CreateRun("massive-scroll");
        MassiveScroll canonical = ModelDb.Relic<MassiveScroll>();
        CardModel[] deckBefore = player.Deck.Cards.ToArray();
        PotionModel?[] potionsBefore = player.PotionSlots.ToArray();
        int goldBefore = player.Gold;

        Assert.False(canonical.IsAllowed(runState));
        Assert.False(canonical.IsAllowedAtNeow(runState));
        await RelicCmd.Obtain(canonical, player);

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Equal(potionsBefore, player.PotionSlots);
        Assert.Equal(goldBefore, player.Gold);
        Assert.Single(player.Relics.OfType<MassiveScroll>());
    }

    [Fact]
    public void Metadata_MatchesAncientWithoutUponPickupPreview()
    {
        MassiveScroll relic = ModelDb.Relic<MassiveScroll>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
    }

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
