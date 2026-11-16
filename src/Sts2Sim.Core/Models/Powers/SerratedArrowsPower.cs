using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>攻击带有己方/任何 ImpaledPower 的敌人时提高伤害（原版 <c>SerratedArrowsPower</c>）。</summary>
public sealed class SerratedArrowsPower : PowerModel
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
        if (!props.IsPoweredAttack())
            return 1m;
        if (!ReferenceEquals(dealer, Owner) && !Owner.Pets.Contains(dealer))
            return 1m;
        if (target is null || !target.Powers.OfType<ImpaledPower>().Any())
            return 1m;
        return 1m + (decimal)Amount / 100m;
    }
}
