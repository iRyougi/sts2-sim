using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ThunderPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterOrbEvoked(OrbModel orb, IEnumerable<Creature> targets)
    {
        if (orb.Owner != Owner.Player || orb is not LightningOrb) return;
        List<Creature> livingTargets = targets.Where(target => target.IsAlive).ToList();
        await CreatureCmd.Damage(Owner.CombatState!, livingTargets, Amount,
            ValueProp.Unpowered, Owner, null, null);
    }
}
