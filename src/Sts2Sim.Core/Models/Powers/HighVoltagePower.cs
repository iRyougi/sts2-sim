namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

public sealed class HighVoltagePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        participants.Contains(Owner)
            ? PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Amount, Owner, null)
            : Task.CompletedTask;
}
