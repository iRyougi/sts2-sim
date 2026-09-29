using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class BlurPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool ShouldClearBlock(Creature creature) => creature != Owner;

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants) =>
        participants.Contains(Owner)
            ? PowerCmd.TickDownDuration(Owner.CombatState!, this)
            : Task.CompletedTask;
}
