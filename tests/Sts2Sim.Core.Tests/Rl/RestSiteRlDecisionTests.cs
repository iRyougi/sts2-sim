using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rl;

[Collection("ModelDb")]
public sealed class RestSiteRlDecisionTests : IDisposable
{
    public RestSiteRlDecisionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    /// <summary>
    /// Deviation #188: the starter deck's duplicate Strikes/Defends collapse to one class each, so
    /// this uses a single freshly-added card (after neutralizing the starter deck via
    /// <see cref="UpgradeEveryCard"/>) to keep the before/after candidate counts unambiguous.
    /// </summary>
    [Fact]
    public void EncodeRestSiteDecision_RebuildsPackedCandidatesFromCurrentUpgradeState()
    {
        (RunState runState, Player player) = NewRun("rest-encode-live");
        UpgradeEveryCard(player);
        CardModel upgradable = AddDeckCard<StrikeRegent>(player);
        int upgradedDeckIndex = DeckIndexOf(player, upgradable);

        ObservationSnapshot before = ObservationEncoder.EncodeRestSiteDecision(runState, player);
        upgradable.Upgrade();
        ObservationSnapshot after = ObservationEncoder.EncodeRestSiteDecision(runState, player);

        Assert.Equal(DecisionType.RestSite, before.DecisionType);
        Assert.Equal(2, before.Candidates.Count);
        Assert.Single(after.Candidates);
        Assert.Equal("heal", after.Candidates[0].Label);
        Assert.DoesNotContain(
            $"smith:{upgradedDeckIndex}:{upgradable.Id}",
            after.Candidates.Select(candidate => candidate.Label));
        Assert.Equal(
            Enumerable.Range(0, after.Candidates.Count).Select(ActionSpaceLayout.RestSiteIndex),
            after.Candidates.Select(candidate => candidate.SlotIndex));
        Assert.Equal(after.Candidates.Count, after.LegalActionMask.Count(isLegal => isLegal));
        Assert.False(after.LegalActionMask[ActionSpaceLayout.RestSiteIndex(after.Candidates.Count)]);
    }

    /// <summary>
    /// Deviation #188: two decks-instances with the same type, upgrade level, and enchantment are
    /// the same decision-equivalence class, so only the first collapses into a candidate; a
    /// differently-enchanted duplicate is a materially different choice and stays distinct.
    /// </summary>
    [Fact]
    public async Task EncodeAndDecodeRestSiteDecision_CollapsesIdenticalDuplicatesButKeepsEnchantedCopyDistinct()
    {
        (RunState runState, Player player) = NewRun("rest-duplicate-references");
        UpgradeEveryCard(player);
        CardModel first = AddDeckCard<StrikeRegent>(player);
        AddDeckCard<StrikeRegent>(player); // identical duplicate: folds into the `first` candidate
        CardModel enchanted = AddDeckCard<StrikeRegent>(player);
        await CardCmd.Enchant<Sharp>(enchanted, 3m);

        ObservationSnapshot snapshot = ObservationEncoder.EncodeRestSiteDecision(runState, player);

        int firstDeckIndex = DeckIndexOf(player, first);
        int enchantedDeckIndex = DeckIndexOf(player, enchanted);
        Assert.Equal(
            ["heal", $"smith:{firstDeckIndex}:{first.Id}", $"smith:{enchantedDeckIndex}:{enchanted.Id}"],
            snapshot.Candidates.Select(candidate => candidate.Label));
        Assert.Same(
            first,
            Assert.IsType<RestSiteDecision.Smith>(
                ActionDecoder.DecodeRestSiteChoice(ActionSpaceLayout.RestSiteIndex(1), player)).Card);
        Assert.Same(
            enchanted,
            Assert.IsType<RestSiteDecision.Smith>(
                ActionDecoder.DecodeRestSiteChoice(ActionSpaceLayout.RestSiteIndex(2), player)).Card);
    }

    private static int DeckIndexOf(Player player, CardModel card) =>
        player.Deck.Cards
            .Select((deckCard, index) => (deckCard, index))
            .Single(candidate => ReferenceEquals(candidate.deckCard, card))
            .index;

    [Fact]
    public void EncodeAndDecodeRestSiteDecision_ToggleHatchFromPersistentEgg()
    {
        (RunState runState, Player player) = NewRun("rest-hatch-toggle");
        UpgradeEveryCard(player);

        ObservationSnapshot withoutEgg = ObservationEncoder.EncodeRestSiteDecision(runState, player);
        AddDeckCard<ByrdonisEgg>(player);
        ObservationSnapshot withEgg = ObservationEncoder.EncodeRestSiteDecision(runState, player);

        Assert.Collection(withoutEgg.Candidates, candidate => Assert.Equal("heal", candidate.Label));
        Assert.Equal(["heal", "hatch"], withEgg.Candidates.Select(candidate => candidate.Label));
        Assert.IsType<RestSiteDecision.Heal>(
            ActionDecoder.DecodeRestSiteChoice(ActionSpaceLayout.RestSiteIndex(0), player));
        Assert.IsType<RestSiteDecision.Hatch>(
            ActionDecoder.DecodeRestSiteChoice(ActionSpaceLayout.RestSiteIndex(1), player));
    }

    [Fact]
    public void DecodeRestSiteChoice_RejectsWrongRangeAndUnusedSlot()
    {
        (_, Player player) = NewRun("rest-invalid-actions");
        UpgradeEveryCard(player);

        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeRestSiteChoice(
            ActionSpaceLayout.ShopIndex(0),
            player));
        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeRestSiteChoice(
            ActionSpaceLayout.RestSiteIndex(1),
            player));
    }

    /// <summary>
    /// Deviation #188: overflow is measured in decision-equivalence classes, not raw deck
    /// instances, so each added card carries a distinct Sharp magnitude — otherwise the identical
    /// duplicates would collapse into one candidate and never reach the static capacity.
    /// </summary>
    [Fact]
    public async Task EncodeAndDecodeRestSiteDecision_FailBeforeTruncatingDeckOverflow()
    {
        (RunState runState, Player player) = NewRun("rest-overflow");
        UpgradeEveryCard(player);
        for (int index = 0; index <= ActionSpaceLayout.MaxDeckBackedChoices; index++)
        {
            CardModel card = AddDeckCard<StrikeRegent>(player);
            await CardCmd.Enchant<Sharp>(card, index + 1);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ObservationEncoder.EncodeRestSiteDecision(runState, player));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActionDecoder.DecodeRestSiteChoice(ActionSpaceLayout.RestSiteIndex(0), player));
    }

    private static (RunState RunState, Player Player) NewRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static void UpgradeEveryCard(Player player)
    {
        foreach (CardModel card in player.Deck.Cards)
        {
            while (card.IsUpgradable)
            {
                card.Upgrade();
            }
        }
    }

    private static CardModel AddDeckCard<TCard>(Player player)
        where TCard : CardModel
    {
        CardModel card = (CardModel)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.Deck.AddInternal(card);
        return card;
    }
}
