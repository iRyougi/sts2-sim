using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class RegenPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEndEarly(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.IsDead)
        {
            return;
        }

        await CreatureCmd.Heal(Owner, Amount);
        await PowerCmd.TickDownDuration(Owner.CombatState!, this);
    }
}
