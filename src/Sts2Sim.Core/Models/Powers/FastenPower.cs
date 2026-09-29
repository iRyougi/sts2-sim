using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class FastenPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return target == Owner &&
               (cardSource is null || cardSource.Tags.Contains(CardTag.Defend)) &&
               !props.HasFlag(ValueProp.Unpowered)
            ? Amount
            : 0m;
    }
}
