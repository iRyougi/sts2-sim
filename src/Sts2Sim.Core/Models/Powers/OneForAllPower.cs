using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class OneForAllPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.IsPoweredAttack() || cardSource is null || cardSource.Owner.Creature != Owner)
            return 0m;
        CardModel playCard = cardPlay?.Card ?? cardSource;
        if (playCard.CostsXEnergy) return 0m;
        if (cardPlay is not null && cardPlay.Resources.EnergySpent != 0) return 0m;
        if (cardPlay is null && cardSource.EnergyCost != 0) return 0m;
        return Amount;
    }
}
