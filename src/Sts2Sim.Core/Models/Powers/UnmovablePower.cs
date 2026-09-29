using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class UnmovablePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyBlockMultiplicative(
        Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target.IsMonster || !props.HasFlag(ValueProp.Move) ||
            (cardSource is not null && !ReferenceEquals(cardSource.Owner.Creature, Owner)))
            return 1m;
        int previous = Owner.CombatState is CombatState state
            ? state.CountOtherQualifyingBlockGainsThisTurn(Owner, cardPlay)
            : 0;
        return previous >= Amount ? 1m : 2m;
    }
}
