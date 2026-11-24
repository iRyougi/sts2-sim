using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class NeowsBones : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task AfterObtained()
    {
        using IDisposable rngScope = Owner.PlayerRng.BeginSemanticScope(
            $"{Owner.CurrentSemanticLocationKey}/noncombat/NeowsBones");
        if (Owner.RunState.CurrentRoom is not EventRoom { Event: AncientEventModel ancient })
        {
            throw new InvalidOperationException(
                "NeowsBones can only be obtained from the current Ancient event.");
        }

        List<RelicModel> candidates = ancient.AllPossibleOptions
            .Where(relic => relic is not NeowsBones && relic.IsAllowedAtNeow(Owner.RunState))
            .ToList();
        candidates.UnstableShuffle(Owner.PlayerRng.NonCombatGeneration);

        // Native offers both mandatory relics together. Nested offers from taking one
        // relic must finish before the next relic or the subsequent curse is resolved.
        Reward[] relicRewards = candidates.Take(2)
            .Select(candidate => (Reward)new RelicReward((RelicModel)candidate.MutableClone(), Owner))
            .ToArray();
        // 游戏：new RewardsSet(Owner).WithCustomRewards(rewards).WithSkippingDisallowed().Offer()（#1094）。
        OfferRewards(RewardsSet.CreateCustom(Owner, extraRewards: relicRewards, skippingDisallowed: true));

        // The queue resumes this continuation after the relic set and its children. 游戏在遗物界面结束后直接把诅咒
        // 加进牌组，没有界面；这里的诅咒奖励同样不可跳过，自己构造界面的驱动方应直接领取、不产生决策（#1094）。
        OfferRewards(RewardsSet.CreateCustom(
            Owner,
            extraRewards: new Reward[] { new NeowsBonesCurseReward(Owner) },
            skippingDisallowed: true));
        return Task.CompletedTask;
    }
}
