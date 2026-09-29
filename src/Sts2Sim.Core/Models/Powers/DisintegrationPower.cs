namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class DisintegrationPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        side == Owner.Side
            ? CreatureCmd.Damage(Owner.CombatState!, [Owner], Amount,
                ValueProp.Unpowered, null, null, null)
            : Task.CompletedTask;
}
