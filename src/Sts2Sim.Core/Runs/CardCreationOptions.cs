using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Runs;

/// <summary>Authoritative reward pool, odds, suppression flags and optional event RNG.</summary>
public sealed class CardCreationOptions(
    IEnumerable<CardPoolModel> cardPools,
    CardCreationSource source,
    CardRarityOddsType rarityOdds,
    Func<CardModel, bool>? cardPoolFilter = null)
{
    public IReadOnlyList<CardPoolModel> CardPools { get; private set; } = cardPools.ToArray();
    public CardCreationSource Source { get; } = source;
    public CardRarityOddsType RarityOdds { get; private set; } = rarityOdds;
    public Func<CardModel, bool>? CardPoolFilter { get; private set; } = cardPoolFilter;
    public CardCreationFlags Flags { get; private set; }
    public Rng? RngOverride { get; private set; }

    public static CardCreationOptions ForNonCombatWithDefaultOdds(
        IEnumerable<CardPoolModel> pools, Func<CardModel, bool>? filter = null) =>
        new CardCreationOptions(pools, CardCreationSource.Other, CardRarityOddsType.RegularEncounter, filter)
            .WithFlags(CardCreationFlags.NoUpgradeRoll);

    public static CardCreationOptions ForNonCombatWithUniformOdds(
        IEnumerable<CardPoolModel> pools, Func<CardModel, bool>? filter = null) =>
        new CardCreationOptions(pools, CardCreationSource.Other, CardRarityOddsType.Uniform, filter)
            .WithFlags(CardCreationFlags.NoUpgradeRoll);

    public IEnumerable<CardModel> GetPossibleCards(Player player) => CardPools
        .SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1))
        .Where(card => CardPoolFilter?.Invoke(card) ?? true);

    public CardCreationOptions WithFlags(CardCreationFlags flags) { Flags |= flags; return this; }
    public CardCreationOptions WithRngOverride(Rng rng) { RngOverride = rng; return this; }
    public CardCreationOptions WithFilter(Func<CardModel, bool> filter) { CardPoolFilter = filter; return this; }
    public CardCreationOptions WithCardPools(IEnumerable<CardPoolModel> pools) { CardPools = pools.ToArray(); return this; }
    public CardCreationOptions WithRarityOdds(CardRarityOddsType odds) { RarityOdds = odds; return this; }
}
