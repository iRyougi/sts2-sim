using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rl;

public class ShopRlDecisionTests : IDisposable
{
    public ShopRlDecisionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void EncodeShopDecision_UsesOrderedTypedLabelsAndPackedMask()
    {
        (RunState runState, Player player, MerchantInventory inventory) = NewShop("shop-encode-order");
        player.Gold = 99_999;

        ObservationSnapshot snapshot = ObservationEncoder.EncodeShopDecision(runState, inventory, player);

        Assert.Equal(DecisionType.Shop, snapshot.DecisionType);
        Assert.Equal(7, inventory.Cards.Count);
        Assert.Equal(3, inventory.Relics.Count);
        Assert.Equal(3, inventory.Potions.Count);
        Assert.Equal(
            $"buy_card:0:{inventory.Cards[0].Card.Id}:price={inventory.Cards[0].Price}",
            snapshot.Candidates[0].Label);
        Assert.Equal(
            $"buy_relic:0:{inventory.Relics[0].Relic.Id}:price={inventory.Relics[0].Price}",
            snapshot.Candidates[7].Label);
        Assert.Equal(
            $"buy_potion:0:{inventory.Potions[0].Potion.Id}:price={inventory.Potions[0].Price}",
            snapshot.Candidates[10].Label);

        CardModel firstRemovable = player.Deck.Cards.First(card => card.IsRemovable);
        int firstRemovalPosition = 13;
        int firstRemovalDeckIndex = player.Deck.Cards
            .Select((card, index) => (card, index))
            .Single(candidate => ReferenceEquals(candidate.card, firstRemovable))
            .index;
        Assert.Equal(
            $"remove_card:{firstRemovalDeckIndex}:{firstRemovable.Id}:price={inventory.CardRemoval.Price}",
            snapshot.Candidates[firstRemovalPosition].Label);
        Assert.Equal("leave", snapshot.Candidates[^1].Label);
        Assert.Equal(
            Enumerable.Range(0, snapshot.Candidates.Count).Select(ActionSpaceLayout.ShopIndex),
            snapshot.Candidates.Select(candidate => candidate.SlotIndex));
        Assert.Equal(snapshot.Candidates.Count, snapshot.LegalActionMask.Count(isLegal => isLegal));
        Assert.False(snapshot.LegalActionMask[ActionSpaceLayout.ShopIndex(snapshot.Candidates.Count)]);
    }

    [Fact]
    public void EncodeShopDecision_ExcludesUnaffordablePurchasedAndFullPotionSlotActions()
    {
        (RunState runState, Player player, MerchantInventory inventory) = NewShop("shop-encode-legality");
        player.Gold = 0;

        ObservationSnapshot unaffordable = ObservationEncoder.EncodeShopDecision(runState, inventory, player);

        CandidateSlot leave = Assert.Single(unaffordable.Candidates);
        Assert.Equal(ActionSpaceLayout.ShopIndex(0), leave.SlotIndex);
        Assert.Equal("leave", leave.Label);

        player.Gold = 99_999;
        inventory.Cards[0].MarkPurchased();
        inventory.CardRemoval.MarkPurchased();
        while (player.PotionSlots.Contains(null))
        {
            player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        }

        ObservationSnapshot constrained = ObservationEncoder.EncodeShopDecision(runState, inventory, player);

        Assert.DoesNotContain(
            constrained.Candidates,
            candidate => candidate.Label.Contains(inventory.Cards[0].Card.Id.ToString()) &&
                         candidate.Label.StartsWith("buy_card:"));
        Assert.DoesNotContain(constrained.Candidates, candidate => candidate.Label.StartsWith("buy_potion:"));
        Assert.DoesNotContain(constrained.Candidates, candidate => candidate.Label.StartsWith("remove_card:"));
        Assert.Equal("leave", constrained.Candidates[^1].Label);
    }

    /// <summary>
    /// Deviation #188: the Regent starter deck has duplicate Strikes and Defends, so "every
    /// removable reference" now means one representative per decision-equivalence class (same
    /// card type, upgrade level, and enchantment) rather than one per raw deck instance.
    /// </summary>
    [Fact]
    public void EncodeShopDecision_OffersOneRemovalPerDeckEquivalenceClassAtTheCurrentFee()
    {
        (RunState runState, Player player, MerchantInventory inventory) = NewShop("shop-encode-removal");
        player.Gold = inventory.CardRemoval.Price;
        var seenClasses = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<(CardModel Card, int DeckIndex)> expected = player.Deck.Cards
            .Select((card, index) => (Card: card, DeckIndex: index))
            .Where(candidate => candidate.Card.IsRemovable)
            .Where(candidate => seenClasses.Add(CardEquivalenceKey.For(candidate.Card)))
            .ToList();

        ObservationSnapshot snapshot = ObservationEncoder.EncodeShopDecision(runState, inventory, player);
        IReadOnlyList<CandidateSlot> removals = snapshot.Candidates
            .Where(candidate => candidate.Label.StartsWith("remove_card:"))
            .ToList();

        Assert.Equal(expected.Count, removals.Count);
        Assert.Equal(
            expected.Select(candidate =>
                $"remove_card:{candidate.DeckIndex}:{candidate.Card.Id}:price={inventory.CardRemoval.Price}"),
            removals.Select(candidate => candidate.Label));
    }

    [Fact]
    public void DecodeShopChoice_ReturnsExactStockDeckAndLeaveReferences()
    {
        (_, Player player, MerchantInventory inventory) = NewShop("shop-decode-references");
        player.Gold = 99_999;
        IReadOnlyList<CardModel> removable = player.Deck.Cards.Where(card => card.IsRemovable).ToList();
        int removalClassCount = removable
            .Select(CardEquivalenceKey.For)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var card = Assert.IsType<ShopDecision.BuyCard>(
            ActionDecoder.DecodeShopChoice(ActionSpaceLayout.ShopIndex(0), inventory, player));
        var relic = Assert.IsType<ShopDecision.BuyRelic>(
            ActionDecoder.DecodeShopChoice(ActionSpaceLayout.ShopIndex(7), inventory, player));
        var potion = Assert.IsType<ShopDecision.BuyPotion>(
            ActionDecoder.DecodeShopChoice(ActionSpaceLayout.ShopIndex(10), inventory, player));
        var removal = Assert.IsType<ShopDecision.BuyCardRemoval>(
            ActionDecoder.DecodeShopChoice(ActionSpaceLayout.ShopIndex(13), inventory, player));
        ShopDecision leave = ActionDecoder.DecodeShopChoice(
            ActionSpaceLayout.ShopIndex(13 + removalClassCount),
            inventory,
            player);

        Assert.Same(inventory.Cards[0], card.Entry);
        Assert.Same(inventory.Relics[0], relic.Entry);
        Assert.Same(inventory.Potions[0], potion.Entry);
        Assert.Same(removable[0], removal.CardToRemove);
        Assert.IsType<ShopDecision.Leave>(leave);
    }

    [Fact]
    public void DecodeShopChoice_RejectsWrongRangeAndUnusedSlot()
    {
        (_, Player player, MerchantInventory inventory) = NewShop("shop-decode-invalid");
        player.Gold = 0;

        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeShopChoice(
            ActionSpaceLayout.MapPointIndex(0),
            inventory,
            player));
        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeShopChoice(
            ActionSpaceLayout.ShopIndex(1),
            inventory,
            player));
    }

    /// <summary>
    /// Deviation #188: overflow is measured in decision-equivalence classes, not raw deck
    /// instances, so each added card carries a distinct Sharp magnitude — identical duplicates
    /// would otherwise collapse into one candidate and never reach the static capacity.
    /// </summary>
    [Fact]
    public async Task EncodeAndDecodeShopChoice_FailBeforeTruncatingDeckOverflow()
    {
        (RunState runState, Player player, MerchantInventory inventory) = NewShop("shop-overflow");
        player.Gold = 99_999;
        for (int index = 0; index <= ActionSpaceLayout.MaxDeckBackedChoices; index++)
        {
            var card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();
            card.AssignOwner(player);
            player.Deck.AddInternal(card);
            await CardCmd.Enchant<Sharp>(card, index + 1);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ObservationEncoder.EncodeShopDecision(runState, inventory, player));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActionDecoder.DecodeShopChoice(ActionSpaceLayout.ShopIndex(0), inventory, player));
    }

    private static (RunState runState, Player player, MerchantInventory inventory) NewShop(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player, MerchantInventory.Generate(player));
    }
}
