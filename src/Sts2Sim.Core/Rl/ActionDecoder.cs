using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// Resolves flat positional action indices using the same hand, enemy, and map-option ordering as
/// <see cref="ObservationEncoder"/>.
/// </summary>
public static class ActionDecoder
{
    public static CardModel DecodeCardSelectionChoice(
        int actionIndex,
        IReadOnlyList<CardModel> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (!ActionSpaceLayout.TryDecodeCardSelection(actionIndex, out int position) ||
            position >= candidates.Count)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} is not a legal card-selection choice for {candidates.Count} candidates.",
                nameof(actionIndex));
        }

        return candidates[position];
    }

    public static MapPoint DecodeMapChoice(int actionIndex, IReadOnlyList<MapPoint> options)
    {
        if (!ActionSpaceLayout.TryDecodeMapPoint(actionIndex, out int position) || position >= options.Count)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} is not a legal map-point choice for {options.Count} options.",
                nameof(actionIndex));
        }

        return options[position];
    }

    public static EventOption DecodeEventChoice(int actionIndex, IReadOnlyList<EventOption> options)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Count, ActionSpaceLayout.MaxEventChoices);
        if (!ActionSpaceLayout.TryDecodeEvent(actionIndex, out int position) || position >= options.Count)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} is not a legal event choice for {options.Count} options.",
                nameof(actionIndex));
        }

        // 与 ObservationEncoder.EncodeEventDecision 的掩码同判据：锁定选项占槽位但不合法。
        EventOption option = options[position];
        if (option.IsLocked)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} selects locked event option '{option.Key}'.",
                nameof(actionIndex));
        }

        return option;
    }

    public static RewardDecision DecodeRewardChoice(int actionIndex, RewardsSet rewards)
    {
        RewardDecisionClassification classification = RewardDecisionClassifier.Classify(rewards);
        if (classification is not RewardDecisionClassification.Choice choice)
        {
            throw new InvalidOperationException("The current reward step has no external choice.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            choice.Candidates.Count,
            ActionSpaceLayout.MaxTotalRewardChoices);
        if (!ActionSpaceLayout.TryDecodeReward(actionIndex, out int position) ||
            position >= choice.Candidates.Count)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} is not a legal reward choice for {choice.Candidates.Count} candidates.",
                nameof(actionIndex));
        }

        return choice.Candidates[position];
    }

    public static ShopDecision DecodeShopChoice(
        int actionIndex,
        MerchantInventory inventory,
        Player player)
    {
        IReadOnlyList<ShopDecisionCandidates.Candidate> candidates =
            ShopDecisionCandidates.Build(inventory, player);
        if (!ActionSpaceLayout.TryDecodeShop(actionIndex, out int position) ||
            position >= candidates.Count)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} is not a legal shop choice for {candidates.Count} candidates.",
                nameof(actionIndex));
        }

        return candidates[position].Decision;
    }

    public static RestSiteDecision DecodeRestSiteChoice(int actionIndex, Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return DecodeRestSiteChoice(
            actionIndex,
            player,
            new Rooms.RestSiteRoom().GetAvailableDecisions(player.RunState, player));
    }

    public static RestSiteDecision DecodeRestSiteChoice(
        int actionIndex,
        Player player,
        IReadOnlyList<RestSiteDecision> decisions)
    {
        IReadOnlyList<RestSiteDecisionCandidates.Candidate> candidates =
            RestSiteDecisionCandidates.Build(player, decisions);
        RestSiteDecisionCandidates.Candidate? candidate = candidates
            .SingleOrDefault(choice => choice.SlotIndex == actionIndex);
        if (candidate is null)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} is not a legal rest-site choice for {candidates.Count} candidates.",
                nameof(actionIndex));
        }

        return candidate.Decision;
    }

    public static CombatDecision DecodeCombatChoice(int actionIndex, CombatState state)
    {
        if (actionIndex == ActionSpaceLayout.EndTurnIndex)
        {
            return new CombatDecision.EndTurn();
        }

        if (ActionSpaceLayout.TryDecodePlayerTarget(actionIndex, out int playerTargetPosition))
        {
            Player targetOwner = state.Players[0];
            IReadOnlyList<CombatTargetCandidates.PlayerCardTarget> playerTargets =
                CombatTargetCandidates.BuildPlayerCardTargets(state, targetOwner);
            if (playerTargets.Count > ActionSpaceLayout.MaxPlayerTargetChoices)
            {
                throw new InvalidOperationException(
                    $"Combat exposes {playerTargets.Count} player-target card actions, exceeding " +
                    $"the static RL capacity {ActionSpaceLayout.MaxPlayerTargetChoices}.");
            }
            if (playerTargetPosition >= playerTargets.Count)
            {
                throw new ArgumentException(
                    $"Action index {actionIndex} is not a legal player-target card action.",
                    nameof(actionIndex));
            }
            CombatTargetCandidates.PlayerCardTarget candidate = playerTargets[playerTargetPosition];
            return new CombatDecision.PlayCard(candidate.Card, candidate.Target);
        }

        if (!ActionSpaceLayout.TryDecodePlayCard(actionIndex, out int handIndex, out int enemyIndex))
        {
            throw new ArgumentException(
                $"Action index {actionIndex} is neither a play-card nor end-turn index.",
                nameof(actionIndex));
        }

        Player player = state.Players[0];
        IReadOnlyList<CardModel> hand = player.PlayerCombatState!.Hand.Cards;
        if (handIndex >= hand.Count)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} refers to hand position {handIndex}, but hand only has {hand.Count} cards.",
                nameof(actionIndex));
        }

        CardModel card = hand[handIndex];
        if (card.TargetType is TargetType.AnyAlly or TargetType.AnyPlayer)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} uses the legacy untargeted slot for a player-target card.",
                nameof(actionIndex));
        }
        if (card.TargetType != TargetType.AnyEnemy)
        {
            if (enemyIndex != 0)
            {
                throw new ArgumentException(
                    $"Action index {actionIndex} uses enemy position {enemyIndex} for a card without an enemy target.",
                    nameof(actionIndex));
            }

            return new CombatDecision.PlayCard(card, Target: null);
        }

        IReadOnlyList<Creature> hittableEnemies = state.HittableEnemies;
        if (enemyIndex >= hittableEnemies.Count)
        {
            throw new ArgumentException(
                $"Action index {actionIndex} refers to enemy position {enemyIndex}, but only {hittableEnemies.Count} enemies are hittable.",
                nameof(actionIndex));
        }

        return new CombatDecision.PlayCard(card, hittableEnemies[enemyIndex]);
    }
}
