using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class NeowsTalismanTests : IDisposable
{
    public NeowsTalismanTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterObtained_UpgradesOnlyLastBasicStrikeAndLastBasicDefend()
    {
        var runState = new RunState("neows-talisman", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        CardModel[] strikes = player.Deck.Cards
            .Where(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike))
            .ToArray();
        CardModel[] defends = player.Deck.Cards
            .Where(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Defend))
            .ToArray();

        await RelicCmd.Obtain(ModelDb.Relic<NeowsTalisman>(), player);

        Assert.All(strikes[..^1], card => Assert.Equal(0, card.CurrentUpgradeLevel));
        Assert.All(defends[..^1], card => Assert.Equal(0, card.CurrentUpgradeLevel));
        Assert.Equal(1, strikes[^1].CurrentUpgradeLevel);
        Assert.Equal(1, defends[^1].CurrentUpgradeLevel);
        NeowsTalisman relic = Assert.Single(player.Relics.OfType<NeowsTalisman>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }
}
