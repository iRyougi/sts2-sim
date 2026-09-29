using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Runs;

/// <summary>A single choice on a reward screen requested by <see cref="RunDriver"/>.</summary>
public abstract record RewardDecision
{
    public sealed record TakeGold : RewardDecision;

    public sealed record TakePotion : RewardDecision;

    public sealed record TakeRelic : RewardDecision;

    public sealed record TakeCard(CardModel Card) : RewardDecision;

    public sealed record SkipCard : RewardDecision;

    public sealed record SelectCardAlternative(CardReward Reward, CardRewardAlternative Alternative) : RewardDecision;

    public sealed record ResolveExtra(Reward Reward, CardModel? SelectedCard = null, bool Skip = false) : RewardDecision;

    public sealed record Done : RewardDecision;
}
