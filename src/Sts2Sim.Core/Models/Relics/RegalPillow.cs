using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RegalPillow : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount) =>
        creature == Owner.Creature ? amount + 15m : amount;

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player == Owner)
        {
            Status = RelicStatus.Normal;
        }

        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        Status = room is RestSiteRoom ? RelicStatus.Active : RelicStatus.Normal;
        return Task.CompletedTask;
    }
}
