using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class DuplicationPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        card.Owner.Creature == Owner ? playCount + 1 : playCount;

    public override Task AfterModifyingCardPlayCount(CardModel card) =>
        PowerCmd.TickDownDuration(Owner.CombatState!, this);

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
