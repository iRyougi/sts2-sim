using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Factories;

/// <summary>Ordered canonical candidates for combat generation and transformation, without RNG draws.</summary>
public static class CardPoolProjection
{
    public static IReadOnlyList<CardModel> OrderedCombatCandidates(Player player, CardPoolModel pool)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(pool);
        return OrderedCombatCandidates(player,
            pool.GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1));
    }

    public static IReadOnlyList<CardModel> OrderedCombatCandidates(Player player, IEnumerable<CardModel> cards)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(cards);
        return FilterCombatCandidates(cards, player.RunState.Players.Count > 1).ToArray();
    }

    internal static IEnumerable<CardModel> FilterCombatCandidates(
        IEnumerable<CardModel> cards, bool isMultiplayer) =>
        (isMultiplayer ? cards : CardPoolFilters.ForSinglePlayer(cards))
            .Where(card => card.CanBeGeneratedInCombat &&
                card.Rarity is not CardRarity.Basic and not CardRarity.Ancient and not CardRarity.Event)
            .Distinct();

    public static IReadOnlyList<CardModel> OrderedTransformationCandidates(
        CardModel original, CardPoolModel pool, bool isInCombat)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(pool);
        return OrderedTransformationCandidates(original,
            pool.GetUnlockedCards(original.Owner.UnlockState, original.Owner.RunState.Players.Count > 1),
            isInCombat);
    }

    public static IReadOnlyList<CardModel> OrderedTransformationCandidates(
        CardModel original, IEnumerable<CardModel> originalOptions, bool isInCombat)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(originalOptions);
        IEnumerable<CardModel> source = originalOptions;
        if (original.Rarity is not (CardRarity.Status or CardRarity.Curse))
            source = source.Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare);
        if (isInCombat) source = source.Where(card => card.CanBeGeneratedInCombat);
        CardModel[] result = source.Where(card => card.Id != original.Id)
            .Where(card => original.Owner.RunState.Players.Count > 1 || !card.IsMultiplayerOnly).ToArray();
        if (result.Length == 0) throw new InvalidOperationException("All transformation options provided are invalid.");
        return result;
    }
}
