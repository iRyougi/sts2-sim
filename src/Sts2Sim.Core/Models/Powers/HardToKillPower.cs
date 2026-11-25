namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

/// <summary>逐字移植 v0.114.0：从 <c>ModifyDamageCap</c> 改为 <c>ModifyHpLostBeforeOstyLate</c>，即在格挡之后封顶失血。
/// 原版的 <c>AfterModifyingHpLostBeforeOsty</c> 只做 <c>Flash()</c>（视觉），省略。</summary>
public sealed class HardToKillPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHpLostBeforeOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!ReferenceEquals(target, Owner))
        {
            return amount;
        }

        if (amount == 0m)
        {
            return amount;
        }

        return Math.Min(amount, Amount);
    }
}
