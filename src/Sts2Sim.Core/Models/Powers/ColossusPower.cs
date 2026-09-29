using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ColossusPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner && props.IsPoweredAttack() && dealer?.HasPower<VulnerablePower>() == true
            ? 0.5m : 1m;
    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        side == CombatSide.Enemy
            ? PowerCmd.TickDownDuration(Owner.CombatState!, this)
            : Task.CompletedTask;
}
