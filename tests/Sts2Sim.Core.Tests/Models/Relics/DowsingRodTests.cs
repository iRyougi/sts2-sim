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
public sealed class DowsingRodTests : IDisposable
{
    public DowsingRodTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterObtained_AddsOneOwnedDowsingToThePersistentDeck()
    {
        var runState = new RunState("dowsing-rod", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        await RelicCmd.Obtain(ModelDb.Relic<DowsingRod>(), player);

        Dowsing card = Assert.Single(player.Deck.Cards.OfType<Dowsing>());
        Assert.False(card.IsCanonical);
        Assert.Same(player, card.Owner);
        Assert.Same(player.Deck, card.Pile);
        DowsingRod relic = Assert.Single(player.Relics.OfType<DowsingRod>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }
}
