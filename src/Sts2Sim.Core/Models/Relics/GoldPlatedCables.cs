using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GoldPlatedCables : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override int ModifyOrbPassiveTriggerCounts(OrbModel orb, int triggerCount)
    {
        if (orb.Owner != Owner || orb != Owner.PlayerCombatState!.OrbQueue.Orbs[0])
            return triggerCount;
        return triggerCount + 1;
    }

    public override Task AfterModifyingOrbPassiveTriggerCount(OrbModel orb)
    {
        Flash();
        return Task.CompletedTask;
    }
}
