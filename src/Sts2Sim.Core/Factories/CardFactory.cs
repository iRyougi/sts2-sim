using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Factories;

/// <summary>Creates reward card options. Pool-scoped fixed-rarity sources preserve selection and upgrade rolls; legacy fixed-rarity sources remain canonical.
/// The filter overload models default noncombat odds with optional upgrade-roll suppression.</summary>
public static class CardFactory
{
    public static IEnumerable<CardModel> GetDefaultTransformationOptions(CardModel original, bool isInCombat)
    {
        CardPoolModel pool;
        if (original.Type == CardType.Quest ||
            original.Rarity is CardRarity.Event or CardRarity.Ancient or CardRarity.Token || original.IsColorless)
            pool = ColorlessCardPool.Instance;
        else if (original.Rarity == CardRarity.Curse) pool = CurseCardPool.Instance;
        else if (original.Rarity == CardRarity.Status) pool = StatusCardPool.Instance;
        else
            pool = ModelDb.All<CharacterModel>().Select(character => character.CardPool)
                .FirstOrDefault(candidate => candidate.AllCards.Any(card => card.Id == original.Id))
                ?? throw new InvalidOperationException($"No original card pool is registered for {original.Id}.");
        return CardPoolProjection.OrderedTransformationCandidates(original, pool, isInCombat);
    }

    public static CardModel CreateRandomCardForTransform(CardModel original, bool isInCombat, Random.Rng rng) =>
        CreateTransformationCard(original, rng.NextItem(GetDefaultTransformationOptions(original, isInCombat))!);

    public static CardModel CreateRandomCardForTransform(
        CardModel original, IEnumerable<CardModel> options, bool isInCombat, Random.Rng rng) =>
        CreateTransformationCard(original, rng.NextItem(CardPoolProjection.OrderedTransformationCandidates(original, options, isInCombat))!);

    private static CardModel CreateTransformationCard(CardModel original, CardModel canonical)
    {
        var card = (CardModel)canonical.MutableClone();
        card.AssignOwner(original.Owner);
        return card;
    }

    public static IReadOnlyList<CardModel> CreateForReward(Player player, int optionCount, CardCreationOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);
        var selected = new HashSet<ModelId>();
        var cards = new List<CardModel>();
        var rng = options.RngOverride ?? player.PlayerRng.Rewards;
        for (int i = 0; i < optionCount; i++)
        {
            CardCreationOptions selectionOptions = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);
            Random.Rng selectionRng = selectionOptions.RngOverride ?? player.PlayerRng.Rewards;
            var remaining = selectionOptions.GetPossibleCards(player).Where(card => !selected.Contains(card.Id)).Distinct().ToList();
            IEnumerable<CardModel> candidates;
            if (selectionOptions.RarityOdds == CardRarityOddsType.Uniform)
                candidates = remaining.Where(c => c.Rarity is not (CardRarity.Basic or CardRarity.Ancient));
            else
            {
                bool modifyOdds = selectionOptions.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange) ||
                    (selectionOptions.Source == CardCreationSource.Encounter && selectionOptions.RarityOdds is
                        CardRarityOddsType.RegularEncounter or CardRarityOddsType.EliteEncounter or CardRarityOddsType.BossEncounter);
                CardRarity rolled = modifyOdds
                    ? player.Odds.CardRarity.Roll(selectionOptions.RarityOdds, selectionRng)
                    : player.Odds.CardRarity.RollWithBaseOdds(selectionOptions.RarityOdds, selectionRng);
                CardRarity rarity = GetNextAllowedRarity(rolled, remaining.Select(c => c.Rarity).ToHashSet());
                candidates = remaining.Where(c => c.Rarity == rarity);
            }
            CardModel picked = selectionRng.NextItem(candidates)
                ?? throw new InvalidOperationException("No eligible distinct card remains for this reward.");
            selected.Add(picked.Id);
            var card = (CardModel)picked.MutableClone();
            card.AssignOwner(player);
            if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)) RollForUpgrade(player, card, 0m, rng);
            cards.Add(card);
        }
        return ModifyRewardOptions(player.RunState, player, cards, options);
    }

    internal static IReadOnlyList<CardModel> ModifyRewardOptions(
        IRunState runState, Player player, IReadOnlyList<CardModel> cards, CardCreationOptions creationOptions)
    {
        var modified = cards.ToList();
        if (creationOptions.Flags.HasFlag(CardCreationFlags.NoModifyHooks)) return modified.AsReadOnly();

        var modifiers = Hook.TryModifyCardRewardOptions(runState, player, modified, creationOptions, late: false).ToList();
        for (int i = 0; i < modified.Count; i++)
        {
            CardModel? replacement = Hook.TryModifyCardRewardOptionLate(
                runState, player, modified[i], creationOptions, out List<AbstractModel> optionModifiers);
            foreach (AbstractModel modifier in optionModifiers)
            {
                if (!modifiers.Any(existing => ReferenceEquals(existing, modifier))) modifiers.Add(modifier);
            }
            if (replacement is not null) modified[i] = replacement;
        }

        foreach (AbstractModel modifier in Hook.TryModifyCardRewardOptions(runState, player, modified, creationOptions, late: true))
            if (!modifiers.Contains(modifier)) modifiers.Add(modifier);
        IReadOnlyList<CardModel> options = modified.AsReadOnly();
        if (modifiers.Count > 0) Hook.AfterModifyingCardRewardOptions(runState, player, options, modifiers);
        return options;
    }

    /// <summary>Selects exactly one eligible reward option using the niche RNG, cloning before enchantment.</summary>
    public static bool EnchantOneRewardOption<TEnchantment>(Player player, List<CardModel> options, decimal magnitude)
        where TEnchantment : EnchantmentModel
    {
        var canonical = (EnchantmentModel)ModelDb.Get(typeof(TEnchantment));
        var eligible = options.Select((card, index) => (card, index)).Where(item => canonical.CanEnchant(item.card)).ToList();
        if (eligible.Count == 0) return false;
        var selected = player.RunState.Rng.Niche.NextItem(eligible);
        var clone = (CardModel)selected.card.MutableClone();
        clone.AssignOwner(player);
        Commands.CardCmd.Enchant<TEnchantment>(clone, magnitude).GetAwaiter().GetResult();
        options[selected.index] = clone;
        return true;
    }

    public static IReadOnlyList<CardModel> CreateForReward(
        Player player, int optionCount, CardPoolModel pool, CardRarity rarity)
    {
        List<CardModel> remaining = pool.GetUnlockedCards(
                player.UnlockState, player.RunState.Players.Count > 1)
            .Where(card => card.Rarity == rarity).ToList();
        var options = new List<CardModel>();
        for (int i = 0; i < optionCount && remaining.Count > 0; i++)
        {
            CardModel canonical = player.PlayerRng.Rewards.NextItem(remaining)!;
            remaining.Remove(canonical);
            options.Add(CreateRewardOption(player, canonical));
        }
        return options;
    }

    public static IReadOnlyList<CardModel> CreateForReward(
        Player player, int optionCount, CardPoolModel pool, CardRarityOddsType oddsType,
        Func<CardModel, bool> filter, bool noUpgradeRoll)
    {
        var options = new List<CardModel>();
        var selected = new HashSet<CardModel>();
        Random.Rng rng = player.PlayerRng.Rewards;
        for (int i = 0; i < optionCount; i++)
        {
            List<CardModel> allCandidates = pool.GetUnlockedCards(
                    player.UnlockState, player.RunState.Players.Count > 1)
                .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
                .Where(card => filter(card) && !selected.Contains(card))
                .ToList();
            CardRarity rarity = GetNextAllowedRarity(
                player.Odds.CardRarity.RollWithBaseOdds(oddsType, rng),
                allCandidates.Select(card => card.Rarity).ToHashSet());
            List<CardModel> rarityCandidates = allCandidates.Where(card => card.Rarity == rarity).ToList();
            CardModel? canonical = rng.NextItem(rarityCandidates);
            if (canonical is null) continue;
            selected.Add(canonical);
            var option = (CardModel)canonical.MutableClone();
            option.AssignOwner(player);
            if (!noUpgradeRoll) RollForUpgrade(player, option, 0m, rng);
            options.Add(option);
        }
        return options;
    }

    private static CardRarity GetNextAllowedRarity(
        CardRarity startingRarity,
        IReadOnlySet<CardRarity> allowedRarities)
    {
        CardRarity current = startingRarity;
        for (int i = 0; i < 3; i++)
        {
            if (allowedRarities.Contains(current)) return current;
            current = current switch
            {
                CardRarity.Common => CardRarity.Uncommon,
                CardRarity.Uncommon => CardRarity.Rare,
                CardRarity.Rare => CardRarity.Common,
                _ => CardRarity.Common,
            };
        }

        return CardRarity.None;
    }

    /// <summary>Creates distinct canonical card options uniformly from one fixed rarity, without an upgrade roll.</summary>
    public static IReadOnlyList<CardModel> CreateForReward(Player player, int optionCount, CardRarity rarity)
    {
        var options = new List<CardModel>();
        var selectedCanonicals = new HashSet<CardModel>();
        for (int i = 0; i < optionCount; i++)
        {
            List<CardModel> candidates = GetRewardCandidates(player)
                .Where(card =>
                    !card.IsColorless &&
                    card.Rarity == rarity &&
                    !selectedCanonicals.Contains(card))
                .ToList();

            CardModel? picked = player.PlayerRng.Rewards.NextItem(candidates);
            if (picked is not null)
            {
                selectedCanonicals.Add(picked);
                options.Add(picked);
            }
        }

        return options;
    }

    /// <summary>Creates distinct mutable reward options using rarity odds, card selection, then one upgrade roll per option.</summary>
    public static IReadOnlyList<CardModel> CreateForReward(Player player, int optionCount, CardRarityOddsType oddsType)
    {
        var creationOptions = new CardCreationOptions([player.Character.CardPool], CardCreationSource.Encounter, oddsType)
            .WithFlags(CardCreationFlags.IsCardReward | CardCreationFlags.IsFromCombat);
        var options = new List<CardModel>();
        var selectedCanonicals = new HashSet<CardModel>();
        Random.Rng rng = player.PlayerRng.Rewards;
        for (int i = 0; i < optionCount; i++)
        {
            CardCreationOptions modifiedCreation = Hook.ModifyCardRewardCreationOptions(player.RunState, player, creationOptions);
            CardRarity rarity = player.Odds.CardRarity.Roll(modifiedCreation.RarityOdds, rng);
            List<CardModel> candidates = modifiedCreation.GetPossibleCards(player)
                .Where(card =>
                    card.Rarity == rarity &&
                    !selectedCanonicals.Contains(card))
                .ToList();

            CardModel? picked = rng.NextItem(candidates);
            if (picked is not null)
            {
                selectedCanonicals.Add(picked);
                var option = (CardModel)picked.MutableClone();
                option.AssignOwner(player);
                RollForUpgrade(player, option, 0m, rng);
                options.Add(option);
            }
        }

        return options;
    }

    private static CardModel CreateRewardOption(Player player, CardModel canonical)
    {
        var option = (CardModel)canonical.MutableClone();
        option.AssignOwner(player);
        RollForUpgrade(player, option, baseChance: 0m);
        return option;
    }

    // Merchant generation consumes the upgrade roll even though its base probability is negative.
    internal static void RollForMerchantUpgrade(Player player, CardModel card) =>
        RollForUpgrade(player, card, -999999999m);

    private static void RollForUpgrade(Player player, CardModel card, decimal baseChance)
        => RollForUpgrade(player, card, baseChance, player.PlayerRng.Rewards);

    private static void RollForUpgrade(Player player, CardModel card, decimal baseChance, Random.Rng rng)
    {
        decimal roll = (decimal)rng.NextFloat();
        if (!card.IsUpgradable)
        {
            return;
        }

        decimal chance = baseChance;
        if (card.Rarity != CardRarity.Rare)
        {
            decimal scaling = player.RunState.Ascension.GetValueIfAscension(
                AscensionLevel.Scarcity,
                0.125m,
                0.25m);
            int currentActIndex = player.RunState is RunState concreteRunState
                ? concreteRunState.CurrentActIndex
                : 0;
            chance += currentActIndex * scaling;
        }

        if (roll <= chance)
        {
            card.Upgrade();
        }
    }
    private static IEnumerable<CardModel> GetRewardCandidates(Player player) =>
        player.Character.CardPool.GetUnlockedCards(
            player.UnlockState,
            isMultiplayer: player.RunState.Players.Count > 1);

    /// <summary>
    /// Creates combat cards from one supplied unlocked pool. Repeats are allowed, matching effects such as Calamity.
    /// </summary>
    public static IReadOnlyList<CardModel> GetForCombat(
        Player player,
        IEnumerable<CardModel> cards,
        int count,
        Sts2Sim.Core.Random.Rng rng)
    {
        List<CardModel> candidates = CardPoolProjection.OrderedCombatCandidates(player, cards).ToList();
        var generated = new List<CardModel>(Math.Max(0, count));
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            CardModel canonical = rng.NextItem(candidates)!;
            var card = (CardModel)canonical.MutableClone();
            card.AssignOwner(player);
            generated.Add(card);
        }

        return generated;
    }

    /// <summary>
    /// Creates distinct combat cards after shuffling the complete eligible pool once. A one-card
    /// request therefore consumes exactly eligible-count minus one RNG draws, matching TakeRandom.
    /// </summary>
    public static IReadOnlyList<CardModel> GetDistinctForCombat(
        Player player,
        IEnumerable<CardModel> cards,
        int count,
        Sts2Sim.Core.Random.Rng rng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        List<CardModel> candidates = CardPoolProjection.OrderedCombatCandidates(player, cards).ToList();
        rng.Shuffle(candidates);
        return candidates.Take(count)
            .Select(canonical =>
            {
                var card = (CardModel)canonical.MutableClone();
                card.AssignOwner(player);
                return card;
            })
            .ToList();
    }
}
