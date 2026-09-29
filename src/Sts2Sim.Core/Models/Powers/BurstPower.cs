using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class BurstPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        card.Owner.Creature == Owner && card.Type == CardType.Skill ? playCount + 1 : playCount;

    public override Task AfterModifyingCardPlayCount(CardModel card) =>
        PowerCmd.TickDownDuration(Owner.CombatState!, this);

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        participants.Contains(Owner) ? PowerCmd.Remove(this) : Task.CompletedTask;
}
