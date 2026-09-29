using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// Describes whether the next unresolved reward step is deterministic or requires an external
/// card/skip choice.
/// </summary>
public abstract record RewardDecisionClassification
{
    public sealed record Automatic(RewardDecision Decision) : RewardDecisionClassification;

    public sealed record Choice(IReadOnlyList<RewardDecision> Candidates) : RewardDecisionClassification;
}

/// <summary>
/// Classifies the next reward action without mutating the reward set. The default run policy and
/// external decision bridges share this classifier so forced reward steps cannot drift apart.
/// </summary>
public static class RewardDecisionClassifier
{
    public static RewardDecisionClassification Classify(RewardsSet rewards)
    {
        ArgumentNullException.ThrowIfNull(rewards);

        if (!rewards.Gold.IsResolved)
        {
            return Automatic(new RewardDecision.TakeGold());
        }
        if (rewards.ExtraRewards.FirstOrDefault(reward => !reward.IsResolved && reward is GoldReward) is { } extraGold)
        {
            return ClassifyExtra(extraGold);
        }

        if (rewards.Potion is { IsResolved: false })
        {
            return Automatic(new RewardDecision.TakePotion());
        }
        if (rewards.ExtraRewards.FirstOrDefault(reward => !reward.IsResolved && reward is PotionReward) is { } extraPotion)
        {
            return ClassifyExtra(extraPotion);
        }

        if (rewards.Relic is { IsResolved: false })
        {
            return Automatic(new RewardDecision.TakeRelic());
        }
        if (rewards.ExtraRewards.FirstOrDefault(reward => !reward.IsResolved && reward is RelicReward) is { } extraRelic)
        {
            return ClassifyExtra(extraRelic);
        }

        if (!rewards.Card.IsResolved)
        {
            IReadOnlyList<CardModel> options = EligibleCards(rewards.Card);
            return options.Count == 0 && rewards.Card.Alternatives.Count == 0
                ? Automatic(new RewardDecision.SkipCard())
                : Choice(
                    options.Select<CardModel, RewardDecision>(card => new RewardDecision.TakeCard(card))
                        .Append(new RewardDecision.SkipCard())
                        .Concat(rewards.Card.Alternatives.Select(alternative =>
                            new RewardDecision.SelectCardAlternative(rewards.Card, alternative)))
                        .ToList());
        }

        Reward? extraReward = rewards.ExtraRewards.FirstOrDefault(reward => !reward.IsResolved);
        return ClassifyExtra(extraReward);
    }

    private static RewardDecisionClassification ClassifyExtra(Reward? extraReward) => extraReward switch
    {
        null => Automatic(new RewardDecision.Done()),
        PotionReward { IsOptionalMerchantChoice: true } potion => Choice(potion.CanTake
            ? [new RewardDecision.ResolveExtra(potion), new RewardDecision.ResolveExtra(potion, Skip: true)]
            : [new RewardDecision.ResolveExtra(potion, Skip: true)]),
        TakeableReward => Automatic(new RewardDecision.ResolveExtra(extraReward)),
        CardReward cardReward => ClassifyExtraCard(cardReward),
        _ => throw new InvalidOperationException(
            $"Unsupported extra reward type: {extraReward.GetType().Name}."),
    };

    public static RewardDecision ChooseDefault(RewardsSet rewards) =>
        Classify(rewards) switch
        {
            RewardDecisionClassification.Automatic automatic => automatic.Decision,
            RewardDecisionClassification.Choice choice => choice.Candidates[0],
            _ => throw new InvalidOperationException("Unsupported reward decision classification."),
        };

    private static RewardDecisionClassification ClassifyExtraCard(CardReward reward)
    {
        IReadOnlyList<CardModel> options = EligibleCards(reward);
        return options.Count == 0 && reward.Alternatives.Count == 0
            ? Automatic(new RewardDecision.ResolveExtra(reward))
            : Choice(
                options.Select<CardModel, RewardDecision>(
                        card => new RewardDecision.ResolveExtra(reward, card))
                    .Append(new RewardDecision.ResolveExtra(reward))
                    .Concat(reward.Alternatives.Select(alternative =>
                        new RewardDecision.SelectCardAlternative(reward, alternative)))
                    .ToList());
    }

    private static IReadOnlyList<CardModel> EligibleCards(CardReward reward) =>
        reward.Options
            .Where(card => !CrossCharacterContentExclusions.IsExcluded(card))
            .ToList();

    private static RewardDecisionClassification.Automatic Automatic(RewardDecision decision) => new(decision);

    private static RewardDecisionClassification.Choice Choice(IReadOnlyList<RewardDecision> candidates) =>
        new(candidates);
}
