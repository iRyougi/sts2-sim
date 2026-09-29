using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class NeowsBones : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task AfterObtained()
    {
        if (Owner.RunState.CurrentRoom is not EventRoom { Event: AncientEventModel ancient })
        {
            throw new InvalidOperationException(
                "NeowsBones can only be obtained from the current Ancient event.");
        }

        List<RelicModel> candidates = ancient.AllPossibleOptions
            .Where(relic => relic is not NeowsBones && relic.IsAllowedAtNeow(Owner.RunState))
            .ToList();
        Owner.PlayerRng.Rewards.Shuffle(candidates);

        // Native offers both mandatory relics together. Nested offers from taking one
        // relic must finish before the next relic or the subsequent curse is resolved.
        Reward[] relicRewards = candidates.Take(2)
            .Select(candidate => (Reward)new RelicReward((RelicModel)candidate.MutableClone(), Owner))
            .ToArray();
        OfferRewards(RewardsSet.CreateCustom(Owner, extraRewards: relicRewards));

        // The queue resumes this continuation after the relic set and its children.
        OfferRewards(RewardsSet.CreateCustom(
            Owner,
            extraRewards: new Reward[] { new NeowsBonesCurseReward(Owner) }));
        return Task.CompletedTask;
    }
}
