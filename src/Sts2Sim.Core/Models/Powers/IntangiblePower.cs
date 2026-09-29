using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// 偏离 #190：权威源码的 <c>ModifyHpLostAfterOsty</c> 在本模拟器没有 Osty 重定向阶段，
/// 因此映射到唯一等价的 <see cref="ModifyHpLost"/> hook。
/// </summary>
public sealed class IntangiblePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHpLost(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        target == Owner && amount >= 1m ? 1m : amount;

    public override decimal ModifyDamageCap(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        target == Owner ? 1m : decimal.MaxValue;

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        side == CombatSide.Enemy
            ? PowerCmd.TickDownDuration(Owner.CombatState!, this)
            : Task.CompletedTask;
}
