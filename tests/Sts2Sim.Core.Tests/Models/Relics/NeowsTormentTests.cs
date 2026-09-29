using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class NeowsTormentTests : IDisposable
{
    public NeowsTormentTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterObtained_AddsOneOwnedNeowsFuryToPermanentDeck()
    {
        Player player = CreatePlayer("neows-torment");
        NeowsFury canonical = ModelDb.Card<NeowsFury>();
        int deckCountBefore = player.Deck.Cards.Count;

        await RelicCmd.Obtain(ModelDb.Relic<NeowsTorment>(), player);

        NeowsFury added = Assert.IsType<NeowsFury>(
            Assert.Single(player.Deck.Cards.Skip(deckCountBefore)));
        Assert.NotSame(canonical, added);
        Assert.True(canonical.IsCanonical);
        Assert.False(added.IsCanonical);
        Assert.Same(player, added.Owner);
        Assert.Same(player.Deck, added.Pile);
    }

    [Fact]
    public void Metadata_MatchesAncientUponPickupBehavior()
    {
        NeowsTorment relic = ModelDb.Relic<NeowsTorment>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
