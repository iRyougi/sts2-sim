using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BlackStar : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (player == Owner && roomType == RoomType.Elite)
        {
            var reward = new RelicReward(player);
            reward.Populate(player.RunState);
            rewards.Add(reward);
        }
    }
}
