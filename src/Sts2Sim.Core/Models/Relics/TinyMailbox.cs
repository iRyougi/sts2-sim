using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TinyMailbox : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool TryModifyRestSiteHealRewards(Player player, List<Reward> rewards, bool isMimicked)
    {
        if (player != Owner) return false;
        rewards.Add(new PotionReward(player));
        rewards.Add(new PotionReward(player));
        return true;
    }
}
