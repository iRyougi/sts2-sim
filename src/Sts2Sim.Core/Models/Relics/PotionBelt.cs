using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PotionBelt : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        Owner.GrowPotionSlots(2);
        return Task.CompletedTask;
    }
}
