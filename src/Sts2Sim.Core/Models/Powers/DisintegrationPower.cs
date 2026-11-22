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
    // Game DisintegrationPower.AfterSideTurnEndLate: participants decide, the owner is the damage dealer.
    public override Task AfterSideTurnEndLate(CombatSide side, IEnumerable<Creature> participants) =>
        participants.Contains(Owner)
            ? CreatureCmd.Damage(Owner.CombatState!, [Owner], Amount,
                ValueProp.Unpowered, Owner, null, null)
            : Task.CompletedTask;
}
