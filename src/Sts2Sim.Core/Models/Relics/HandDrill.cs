using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HandDrill : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterBlockBroken(Creature target, Creature? breaker)
    {
        // Inherited deviation #31: pet/Osty ownership is not represented.
        if (breaker == Owner.Creature && !target.IsPlayer)
            await PowerCmd.Apply<VulnerablePower>(target.CombatState!, target, 2m, Owner.Creature, null);
    }
}
