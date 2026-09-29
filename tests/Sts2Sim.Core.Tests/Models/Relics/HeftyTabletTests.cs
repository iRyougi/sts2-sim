using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class HeftyTabletTests : IDisposable
{
    public HeftyTabletTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    public async Task AfterObtained_OffersThreeRareCandidatesAndAddsChosenCardThenInjury(int chosenIndex, bool bingBong)
    {
        var runState = new RunState("hefty-tablet", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        int deckCountBefore = player.Deck.Cards.Count;
        int rngBefore = player.PlayerRng.Rewards.Counter;
        var selection = new IndexedSelectionSource(chosenIndex);
        runState.ConfigureCardSelectionSource(selection);
        await RelicCmd.Obtain(ModelDb.Relic<Glitter>(), player);
        if (bingBong)
            await RelicCmd.Obtain(ModelDb.Relic<BingBong>(), player);

        await RelicCmd.Obtain(ModelDb.Relic<HeftyTablet>(), player);

        CardModel[] added = player.Deck.Cards.Skip(deckCountBefore).ToArray();
        CardSelectionRequest request = Assert.IsType<CardSelectionRequest>(selection.Request);
        Assert.Equal(3, request.Candidates.Count);
        Assert.Equal((0, 1, true), (request.MinCount, request.MaxCount, request.Cancelable));
        Assert.All(request.Candidates, card => Assert.Equal(CardRarity.Rare, card.Rarity));
        Assert.All(request.Candidates, card => Assert.IsType<Glam>(Assert.Single(card.Enchantments)));
        Assert.Equal((chosenIndex < 0 ? 1 : 2) * (bingBong ? 2 : 1), added.Length);
        if (chosenIndex >= 0)
        {
            Assert.Equal(request.Candidates[chosenIndex].Id, added[0].Id);
            Assert.Equal(request.Candidates[chosenIndex].IsUpgraded, added[0].IsUpgraded);
            Assert.IsType<Glam>(Assert.Single(added[0].Enchantments));
        }
        int injuryIndex = chosenIndex < 0 ? 0 : 1;
        Assert.IsType<Injury>(added[injuryIndex]);
        if (bingBong)
        {
            int originalCount = chosenIndex < 0 ? 1 : 2;
            for (int i = 0; i < originalCount; i++)
            {
                Assert.Equal(added[i].Id, added[i + originalCount].Id);
                Assert.NotSame(added[i], added[i + originalCount]);
            }
        }
        Assert.All(added, card =>
        {
            Assert.False(card.IsCanonical);
            Assert.Same(player, card.Owner);
            Assert.Same(player.Deck, card.Pile);
        });
        Assert.Equal(rngBefore + 3, player.PlayerRng.Rewards.Counter);
        HeftyTablet relic = Assert.Single(player.Relics.OfType<HeftyTablet>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task AfterObtained_WhenRarePoolHasNoCandidates_FailsBeforeDeckMutation()
    {
        HashSet<Type> rareCardTypes = ModelDb.All<CardModel>()
            .Where(card => card.Rarity == CardRarity.Rare)
            .Select(card => card.GetType())
            .ToHashSet();
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Where(type => !rareCardTypes.Contains(type)));
        var runState = new RunState("hefty-tablet-no-candidates", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        int deckCountBefore = player.Deck.Cards.Count;
        int rngBefore = player.PlayerRng.Rewards.Counter;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RelicCmd.Obtain(ModelDb.Relic<HeftyTablet>(), player));

        Assert.Empty(player.Deck.Cards.Skip(deckCountBefore));
        Assert.Equal(rngBefore, player.PlayerRng.Rewards.Counter);
    }

    private sealed class IndexedSelectionSource(int index) : ICardSelectionDecisionSource
    {
        public CardSelectionRequest? Request { get; private set; }

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Request = request;
            IReadOnlyList<CardModel> chosen = index < 0 ? [] : [request.Candidates[index]];
            return Task.FromResult(chosen);
        }
    }
}
