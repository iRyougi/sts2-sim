using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Adds a gold reward equal to this power's amount after combat victory.</summary>
public sealed class RoyaltiesPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (player != Owner.Player || !roomType.IsCombatRoom())
        {
            return;
        }

        var gold = new GoldReward(Amount, player);
        gold.Populate(player.RunState);
        rewards.Add(gold);
    }
}
