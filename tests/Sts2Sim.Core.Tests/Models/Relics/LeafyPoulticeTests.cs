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
public sealed class LeafyPoulticeTests : IDisposable
{
    public LeafyPoulticeTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterObtained_LosesTwelveMaxHpAndTransformsFirstBasicStrikeAndDefend()
    {
        var runState = new RunState("leafy-poultice", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        CardModel firstStrike = player.Deck.Cards.First(card =>
            card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike));
        CardModel firstDefend = player.Deck.Cards.First(card =>
            card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Defend));
        CardModel[] untouchedBasics = player.Deck.Cards
            .Where(card => card.Rarity == CardRarity.Basic &&
                !ReferenceEquals(card, firstStrike) && !ReferenceEquals(card, firstDefend))
            .ToArray();
        int nicheBefore = runState.Rng.Niche.Counter;
        int transformationsBefore = player.PlayerRng.Transformations.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<LeafyPoultice>(), player);

        Assert.Equal(63, player.Creature.MaxHp);
        Assert.Equal(63, player.Creature.CurrentHp);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, firstStrike));
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, firstDefend));
        Assert.All(untouchedBasics, card =>
            Assert.Contains(player.Deck.Cards, candidate => ReferenceEquals(candidate, card)));
        CardModel[] replacements = player.Deck.Cards.Skip(player.Deck.Cards.Count - 2).ToArray();
        Assert.Equal(2, replacements.Length);
        Assert.All(replacements, card =>
        {
            Assert.False(card.IsCanonical);
            Assert.False(card.IsColorless);
            Assert.False(card.IsMultiplayerOnly);
            Assert.True(card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare);
            Assert.Same(player, card.Owner);
            Assert.Same(player.Deck, card.Pile);
        });
        Assert.Equal(nicheBefore, runState.Rng.Niche.Counter);
        Assert.Equal(transformationsBefore + 2, player.PlayerRng.Transformations.Counter);
        LeafyPoultice relic = Assert.Single(player.Relics.OfType<LeafyPoultice>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
    }
}
