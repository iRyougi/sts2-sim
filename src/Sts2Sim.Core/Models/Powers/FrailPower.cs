namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

/// <summary>Reduces powered card or monster-move block gained by the owner to 75 percent.</summary>
public sealed class FrailPower : PowerModel
{
    private const decimal BlockMultiplier = 0.75m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return target == Owner && props.IsPoweredCardOrMonsterMoveBlock()
            ? BlockMultiplier
            : 1m;
    }

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        }
    }
}
