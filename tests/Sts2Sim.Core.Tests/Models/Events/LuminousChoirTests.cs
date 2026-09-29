using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class LuminousChoirTests : IDisposable
{
    public LuminousChoirTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(Sts2Sim.Core.Models.Relics.Anchor),
            typeof(Sts2Sim.Core.Models.Relics.Akabeko),
            typeof(Sts2Sim.Core.Models.Relics.ArtOfWar),
            typeof(Sts2Sim.Core.Models.Relics.BeltBuckle),
            typeof(Sts2Sim.Core.Models.Relics.Circlet),
            typeof(SporeMind),
            typeof(LuminousChoir),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public void IsAllowed_RequiresBaseTributeGoldAndAnyAvailableSupportedRelicBeforeBeginEvent()
    {
        var runState = new RunState("luminous-is-allowed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        LuminousChoir choir = ModelDb.Event<LuminousChoir>();

        player.Gold = 149;
        Assert.True(choir.IsAllowed(runState));

        player.Gold = 148;
        Assert.False(choir.IsAllowed(runState));

        player.Gold = 149;
        while (player.RelicGrabBag.PullFromFront(RelicRarity.Common) is not null)
        {
        }

        Assert.True(choir.IsAllowed(runState));

        while (player.RelicGrabBag.PullFromFront(RelicRarity.Uncommon) is not null ||
               player.RelicGrabBag.PullFromFront(RelicRarity.Rare) is not null ||
               player.RelicGrabBag.PullFromFront(RelicRarity.Shop) is not null)
        {
        }

        Assert.False(choir.IsAllowed(runState));
    }
    [Fact]
    public async Task FromDeckForRemoval_UsesConfiguredPolicy()
    {
        var runState = new RunState("luminous-removal-selection", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        CardModel[] expected = player.Deck.Cards.Where(card => card.IsRemovable).Take(2).ToArray();

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromDeckForRemoval(player, 2, ModelDb.Event<LuminousChoir>());

        Assert.Equal(expected, selected);
    }

    [Fact]
    public async Task RemoveFromDeck_RemovesAnOwnedPersistentDeckCard()
    {
        var runState = new RunState("luminous-remove-command", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        CardModel card = player.Deck.Cards[0];

        await CardPileCmd.RemoveFromDeck(player, card);

        Assert.DoesNotContain(player.Deck.Cards, candidate => ReferenceEquals(candidate, card));
        Assert.Null(card.Pile);
    }

    [Fact]
    public async Task RemoveFromDeck_RejectsACardOwnedByAnotherPlayer()
    {
        var runState = new RunState("luminous-remove-owner", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        runState.AddPlayer(other);
        CardModel card = other.Deck.Cards[0];

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardPileCmd.RemoveFromDeck(player, card));

        Assert.Contains(card, other.Deck.Cards);
    }

    [Fact]
    public async Task RemoveFromDeck_RejectsAnOwnedCardOutsideThePersistentDeck()
    {
        var runState = new RunState("luminous-remove-pile", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        CardModel card = player.Deck.Cards[0];
        CardPileCmd.Remove(card);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardPileCmd.RemoveFromDeck(player, card));
    }

    [Fact]
    public async Task AddCursesToDeck_ClonesCanonicalCards_AssignsOwner_AndUsesPermanentDeckAdd()
    {
        var runState = new RunState("luminous-add-curse", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        CardModel canonical = ModelDb.Card<SporeMind>();

        IReadOnlyList<CardModel> added = await CardPileCmd.AddCursesToDeck(new[] { canonical }, player);

        CardModel curse = Assert.Single(added);
        Assert.NotSame(canonical, curse);
        Assert.IsType<SporeMind>(curse);
        Assert.Same(player, curse.Owner);
        Assert.Same(player.Deck, curse.Pile);
        Assert.Contains(curse, player.Deck.Cards);
    }
    [Fact]
    public async Task AddCursesToDeck_ValidatesTheEntireInputBeforeMutatingTheDeck()
    {
        var runState = new RunState("luminous-add-curse-atomic", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        int deckCountBefore = player.Deck.Cards.Count;

        await Assert.ThrowsAsync<ArgumentException>(() => CardPileCmd.AddCursesToDeck(
            new CardModel[] { ModelDb.Card<SporeMind>(), ModelDb.Card<StrikeRegent>() },
            player));

        Assert.Equal(deckCountBefore, player.Deck.Cards.Count);
        Assert.DoesNotContain(player.Deck.Cards, card => card is SporeMind);
    }
    [Fact]
    public void HasAvailableRelics_AnyRarityIsReadOnlyAndReportsAcrossRewardBuckets()
    {
        var runState = new RunState("luminous-any-relics", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());

        Assert.True(player.RelicGrabBag.HasAvailableRelics());
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Common));
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Uncommon));
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Rare));
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Shop));
        Assert.False(player.RelicGrabBag.HasAvailableRelics());
    }

    [Fact]
    public void PullNextRelicFromFront_RollsPlayerRewardsRarity_AndPullsThatExactBucketFront()
    {
        var observed = new HashSet<RelicRarity>();
        for (int i = 0; i < 100; i++)
        {
            var runState = new RunState($"luminous-relic-rarity-{i}", new Overgrowth());
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
            RelicRarity expected = RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact());

            RelicModel pulled = RelicFactory.PullNextRelicFromFront(player);

            observed.Add(expected);
            Assert.Equal(expected, pulled.Rarity);
        }

        Assert.Equal(
            new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare },
            observed.Order());
    }

    [Fact]
    public void PullNextRelicFromFront_WhenRolledBucketIsEmpty_ReturnsCircletWithoutCrossRarityFallback()
    {
        var runState = new RunState("luminous-relic-empty", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        RelicRarity expected = RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact());
        Assert.NotNull(player.RelicGrabBag.PullFromFront(expected));

        RelicModel pulled = RelicFactory.PullNextRelicFromFront(player);

        Assert.IsType<Sts2Sim.Core.Models.Relics.Circlet>(pulled);
    }
    [Fact]
    public async Task OfferTribute_CostUsesInclusiveRangeOneHundredThroughOneHundredFortyNine()
    {
        var observed = new HashSet<int>();
        for (int i = 0; i < 2_048 && (!observed.Contains(100) || !observed.Contains(149)); i++)
        {
            (_, Player player, LuminousChoir ev) = SetupEvent($"luminous-cost-{i}", 149);

            await Choose(ev, "OFFER_TRIBUTE");

            observed.Add(149 - player.Gold);
        }

        Assert.All(observed, cost => Assert.InRange(cost, 100, 149));
        Assert.Contains(100, observed);
        Assert.Contains(149, observed);
    }

    [Fact]
    public async Task OfferTribute_SpendsRolledCost_AndObtainsTheRolledRarityFrontRelic()
    {
        (_, Player player, LuminousChoir ev) = SetupEvent("luminous-offer", 149);
        RelicRarity expectedRarity = RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact());
        int goldBefore = player.Gold;

        await Choose(ev, "OFFER_TRIBUTE");

        Assert.InRange(goldBefore - player.Gold, 100, 149);
        RelicModel obtained = Assert.Single(player.Relics, relic => relic.Rarity == expectedRarity);
        Assert.Same(player, obtained.Owner);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task OfferTribute_IsHiddenWhenGoldIsBelowTheRolledCost()
    {
        (_, Player fundedPlayer, LuminousChoir funded) = SetupEvent("luminous-balance", 149);
        await Choose(funded, "OFFER_TRIBUTE");
        int cost = 149 - fundedPlayer.Gold;

        (_, _, LuminousChoir underfunded) = SetupEvent("luminous-balance", cost - 1);

        Assert.DoesNotContain(underfunded.CurrentOptions, option => option.Key == "OFFER_TRIBUTE");
        Assert.Contains(underfunded.CurrentOptions, option => option.Key == "REACH_INTO_THE_FLESH");
    }

    [Fact]
    public void OfferTribute_IsHiddenWhenEveryRewardRarityBucketIsEmpty()
    {
        (_, Player player, LuminousChoir ev) = SetupEvent("luminous-empty-pools", 149, begin: false);
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Common));
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Uncommon));
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Rare));
        Assert.NotNull(player.RelicGrabBag.PullFromFront(RelicRarity.Shop));
        ev.BeginEvent(player.RunState);

        Assert.DoesNotContain(ev.CurrentOptions, option => option.Key == "OFFER_TRIBUTE");
    }

    [Fact]
    public async Task ReachIntoTheFlesh_RemovesTheFirstTwoRemovableCards_AndAddsOwnedSporeMind()
    {
        (_, Player player, LuminousChoir ev) = SetupEvent("luminous-reach", 0);
        CardModel[] removed = player.Deck.Cards.Where(card => card.IsRemovable).Take(2).ToArray();
        int countBefore = player.Deck.Cards.Count;

        await Choose(ev, "REACH_INTO_THE_FLESH");

        Assert.All(removed, card => Assert.DoesNotContain(player.Deck.Cards, candidate => ReferenceEquals(candidate, card)));
        Assert.Equal(countBefore - 1, player.Deck.Cards.Count);
        SporeMind curse = Assert.Single(player.Deck.Cards.OfType<SporeMind>());
        Assert.Same(player, curse.Owner);
        Assert.Same(player.Deck, curse.Pile);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task ReachIntoTheFlesh_WithNoRemovableCandidates_StillAddsSporeMindAndFinishes()
    {
        (_, Player player, LuminousChoir ev) = SetupEvent("luminous-reach-empty", 0);
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }

        await Choose(ev, "REACH_INTO_THE_FLESH");

        Assert.IsType<SporeMind>(Assert.Single(player.Deck.Cards));
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, LuminousChoir Event) SetupEvent(
        string seed,
        int gold,
        bool begin = true)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        player.Gold = gold;
        var ev = (LuminousChoir)ModelDb.Event<LuminousChoir>().MutableClone();
        ev.AssignOwner(player);
        if (begin)
        {
            ev.BeginEvent(runState);
        }
        return (runState, player, ev);
    }

    private static Task Choose(LuminousChoir ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
