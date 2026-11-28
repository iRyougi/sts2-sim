using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PotionBelt : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool HasUponPickupEffect => true;

    protected override bool IsAllowedInternal(IRunState runState) =>
        !IsLateInAct3(runState) || runState.GameMode != GameMode.Blitz;

    public override Task AfterObtained()
    {
        Owner.GrowPotionSlots(2);
        return Task.CompletedTask;
    }
}
