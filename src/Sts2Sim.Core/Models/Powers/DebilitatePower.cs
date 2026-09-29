using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>DebilitatePower</c>：把持有者身上易伤/虚弱的效果翻倍（易伤倍率 m → 2m−1，虚弱倍率 m → 2m−1），
/// 只对有力攻击生效；持有者所在一方回合结束时减 1 层。两个倍率方法不是钩子，由原版 <c>VulnerablePower</c>
/// （以受击者身上的本能力）和 <c>WeakPower</c>（以攻击者身上的本能力）在各自的乘法修正末尾直接调用。</summary>
public sealed class DebilitatePower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public decimal ModifyVulnerableMultiplier(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner)
            return amount;
        if (!props.IsPoweredAttack())
            return amount;
        return amount + (amount - 1m);
    }

    public decimal ModifyWeakMultiplier(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner)
            return amount;
        if (!props.IsPoweredAttack())
            return amount;
        return amount - (1m - amount);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
    }
}
