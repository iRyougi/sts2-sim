using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>CalcifyPower</c>：持有者自己的 Osty 造成的有力攻击伤害 +层数（加法修正）。</summary>
public sealed class CalcifyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer?.Monster is not Osty)
            return 0m;
        if (Owner != dealer.PetOwner?.Creature)
            return 0m;
        if (!props.IsPoweredAttack())
            return 0m;
        return Amount;
    }
}
