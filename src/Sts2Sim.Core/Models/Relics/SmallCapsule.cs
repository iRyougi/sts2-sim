using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SmallCapsule : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task AfterObtained()
    {
        var reward = new RelicReward(Owner);
        reward.Populate(Owner.RunState);
        OfferRewards(RewardsSet.CreateCustom(Owner, relic: reward));
        return Task.CompletedTask;
    }
}
