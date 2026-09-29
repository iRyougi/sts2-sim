using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Tracks an actual temporary Strength change until the owner's side ends.</summary>
public class TemporaryStrengthPower : PowerModel, ITemporaryPower
{
    public override PowerType Type => IsPositive ? PowerType.Buff : PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public PowerModel InternallyAppliedPower => ModelDb.Power<StrengthPower>();

    protected virtual bool IsPositive => true;

    private int Sign => IsPositive ? 1 : -1;

    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        await PowerCmd.Apply<StrengthPower>(target.CombatState!, target, Sign * amount, applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        // BeforeApplied already applied the initial amount. Later hooks carry the delta.
        if (amount != Amount && power == this)
        {
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Sign * amount, applier, cardSource);
        }
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, -Sign * Amount, Owner, null);
        }
    }
}
