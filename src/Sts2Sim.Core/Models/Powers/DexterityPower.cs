using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Increases block gained from powered cards or moves owned by this creature.</summary>
public sealed class DexterityPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => true;

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        bool ownedSource = cardSource is not null
            ? cardSource.Owner.Creature == Owner
            : target == Owner;
        return ownedSource && !props.HasFlag(ValueProp.Unpowered) ? Amount : 0m;
    }
}
