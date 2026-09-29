using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class CrueltyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public decimal ModifyVulnerableMultiplier(Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner || !props.IsPoweredAttack()) return amount;
        return amount + Amount / 100m;
    }
}
