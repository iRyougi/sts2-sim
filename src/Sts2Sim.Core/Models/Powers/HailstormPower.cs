using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class HailstormPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        int frostCount = Owner.Player!.PlayerCombatState!.OrbQueue.Orbs.Count(orb => orb is FrostOrb);
        if (frostCount >= 1)
            await CreatureCmd.Damage(Owner.CombatState!, Owner.CombatState!.HittableEnemies,
                Amount, ValueProp.Unpowered, Owner, null, null);
    }
}
