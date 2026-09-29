namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

public sealed class HatchPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        participants.Contains(Owner)
            ? PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null)
            : Task.CompletedTask;
}
