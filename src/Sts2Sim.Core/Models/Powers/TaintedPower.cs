namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class TaintedPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        ReferenceEquals(target, Owner) && props.IsPoweredAttack() ? Amount : 0m;

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        side == CombatSide.Enemy ? PowerCmd.Remove(this) : Task.CompletedTask;
}
