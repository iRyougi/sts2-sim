namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;

/// <summary>Retains the dead Waterfall Giant and its accumulated steam until the stunned turn consumes it.</summary>
public sealed class SteamEruptionPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(Creature target)
    {
        // The legacy callback is dispatched only for AfterDeath(false),
        // matching the source's wasRemovalPrevented == false branch.
        if (target == Owner)
        {
            await ((WaterfallGiant)target.Monster!).TriggerAboutToBlowState();
        }
    }

    public override bool ShouldStopCombatFromEnding() => true;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        creature != Owner;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}
