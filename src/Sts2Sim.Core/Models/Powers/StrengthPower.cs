using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>力量。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.StrengthPower</c>）。</summary>
public sealed class StrengthPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => true;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Owner != dealer)
        {
            return 0m;
        }

        if (!props.IsPoweredAttack())
        {
            return 0m;
        }

        return Amount;
    }
}
