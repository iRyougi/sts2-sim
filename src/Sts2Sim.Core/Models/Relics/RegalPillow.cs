using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RegalPillow : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount) =>
        creature == Owner.Creature ? amount + 15m : amount;
}
