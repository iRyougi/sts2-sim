using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LastingCandy : RelicModel
{
    private int _combatRewardsSeen;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    // The source excludes the first Ironclad run when NumberOfRuns == 0. Our all-unlocked
    // perfect-save assumption implies prior runs, and we do not track cross-run counts.
    // The remaining in-run acquisition gate is exact for the supported save state.
    public override bool IsAllowed(IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public int CombatRewardsSeen
    {
        get => _combatRewardsSeen;
        private set { AssertMutable(); _combatRewardsSeen = value; }
    }

    public override bool TryModifyCardRewardOptions(
        Player player,
        List<CardModel> options,
        CardCreationOptions creationOptions)
    {
        if (!ReferenceEquals(Owner, player) || CombatRewardsSeen <= 0 || CombatRewardsSeen % 2 != 1 ||
            creationOptions.Source != CardCreationSource.Encounter ||
            !creationOptions.Flags.HasFlag(CardCreationFlags.IsCardReward) ||
            !creationOptions.Flags.HasFlag(CardCreationFlags.IsFromCombat))
            return false;

        List<CardModel> candidates = creationOptions.GetPossibleCards(player)
            .Where(card => card.Type == CardType.Power && !options.Any(existing => existing.Id == card.Id))
            .ToList();
        bool duplicateFallback = candidates.Count == 0;
        if (duplicateFallback)
            candidates = creationOptions.GetPossibleCards(player).Where(card => card.Type == CardType.Power).ToList();
        if (candidates.Count == 0)
            return false;

        CardCreationOptions appendedOptions = new CardCreationOptions(
            creationOptions.CardPools,
            CardCreationSource.Other,
            creationOptions.RarityOdds,
            card => (creationOptions.CardPoolFilter?.Invoke(card) ?? true) &&
                    card.Type == CardType.Power &&
                    (duplicateFallback || options.All(existing => existing.Id != card.Id)))
            .WithFlags(CardCreationFlags.NoModifyHooks | CardCreationFlags.NoCardPoolModifications);
        options.Add(CardFactory.CreateForReward(player, 1, appendedOptions).Single());
        return true;
    }

    public override Task BeforeCombatRewardOffered(RewardsSet rewards, CombatRoom room)
    {
        if (ReferenceEquals(rewards.Player, Owner) &&
            (rewards.Card is not null || rewards.ExtraRewards.OfType<CardReward>().Any()))
            CombatRewardsSeen++;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref Combat.StateDescription.CombatStateDescriptionBuilder builder,
        Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_combatRewardsSeen);
}
