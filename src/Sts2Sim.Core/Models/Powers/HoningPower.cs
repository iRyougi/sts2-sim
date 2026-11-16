using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>按百分比提升 FromArrow 攻击伤害（原版 <c>HoningPower</c>）。</summary>
public sealed class HoningPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!ReferenceEquals(dealer, Owner) || !props.IsPoweredAttack() || !props.HasFlag(ValueProp.FromArrow))
            return 1m;
        return 1m + (decimal)Amount / 100m;
    }
}
