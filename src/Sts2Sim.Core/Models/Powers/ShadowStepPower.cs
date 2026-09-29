using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ShadowStepPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        await PowerCmd.Apply<DoubleDamagePower>(Owner.CombatState!, Owner, Amount, Owner, null);
        await PowerCmd.Remove(this);
    }
}
