using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// Rebuilds the current legal shop choices in the positional order shared by observation encoding
/// and action decoding. The list is intentionally never cached because purchases mutate gold,
/// inventory entries, potion capacity, and the removable deck.
/// </summary>
internal static class ShopDecisionCandidates
{
    internal sealed record Candidate(ShopDecision Decision, string Label);

    public static IReadOnlyList<Candidate> Build(MerchantInventory inventory, Player player)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            inventory.Cards.Count,
            ActionSpaceLayout.MaxShopCardChoices);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            inventory.Relics.Count,
            ActionSpaceLayout.MaxShopRelicChoices);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            inventory.Potions.Count,
            ActionSpaceLayout.MaxShopPotionChoices);

        var candidates = new List<Candidate>(ActionSpaceLayout.MaxShopChoices);
        AddStockCandidates(
            candidates,
            inventory.Cards,
            player,
            (entry, index) => new Candidate(
                new ShopDecision.BuyCard(entry),
                $"buy_card:{index}:{entry.Card.Id}:price={entry.Price}"));
        AddStockCandidates(
            candidates,
            inventory.Relics,
            player,
            (entry, index) => new Candidate(
                new ShopDecision.BuyRelic(entry),
                $"buy_relic:{index}:{entry.Relic.Id}:price={entry.Price}"));

        if (player.PotionSlots.Contains(null))
        {
            AddStockCandidates(
                candidates,
                inventory.Potions,
                player,
                (entry, index) => new Candidate(
                    new ShopDecision.BuyPotion(entry),
                    $"buy_potion:{index}:{entry.Potion.Id}:price={entry.Price}"),
                entry => PotionCmd.CanProcure(entry.Potion, player));
        }

        MerchantCardRemovalEntry removal = inventory.CardRemoval;
        if (!removal.Purchased && player.Gold >= removal.Price)
        {
            IReadOnlyList<(CardModel Card, int DeckIndex)> removableCards = player.Deck.Cards
                .Select((card, index) => (Card: card, DeckIndex: index))
                .Where(candidate => candidate.Card.IsRemovable)
                .ToList();

            // Deviation #188: collapse to decision-equivalence classes (same card type, upgrade
            // level, and enchantment — see CardEquivalenceKey) before enforcing capacity. An
            // unbounded deck (e.g. via BingBong) can carry many duplicate instances of the same
            // removable card, but they are one legal "remove a Strike" choice to the RL agent, not
            // one action per physical copy. The first deck instance of each class is kept as the
            // representative so ShopDecision.BuyCardRemoval still resolves against a real card.
            var seenClasses = new HashSet<string>(StringComparer.Ordinal);
            List<(CardModel Card, int DeckIndex)> representativeRemovableCards = removableCards
                .Where(candidate => seenClasses.Add(CardEquivalenceKey.For(candidate.Card)))
                .ToList();
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                representativeRemovableCards.Count,
                ActionSpaceLayout.MaxDeckBackedChoices);

            foreach ((CardModel card, int deckIndex) in representativeRemovableCards)
            {
                candidates.Add(new Candidate(
                    new ShopDecision.BuyCardRemoval(card),
                    $"remove_card:{deckIndex}:{card.Id}:price={removal.Price}"));
            }
        }

        candidates.Add(new Candidate(new ShopDecision.Leave(), "leave"));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            candidates.Count,
            ActionSpaceLayout.MaxShopChoices);
        return candidates;
    }

    private static void AddStockCandidates<TEntry>(
        ICollection<Candidate> candidates,
        IReadOnlyList<TEntry> entries,
        Player player,
        Func<TEntry, int, Candidate> create,
        Func<TEntry, bool>? isAvailable = null)
        where TEntry : MerchantEntry
    {
        for (int index = 0; index < entries.Count; index++)
        {
            TEntry entry = entries[index];
            if (!entry.Purchased &&
                player.Gold >= entry.Price &&
                (isAvailable is null || isAvailable(entry)))
            {
                candidates.Add(create(entry, index));
            }
        }
    }
}
