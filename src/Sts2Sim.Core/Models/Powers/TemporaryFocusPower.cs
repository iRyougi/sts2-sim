using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>A temporary Focus change paired with an ordinary FocusPower stack.</summary>
public abstract class TemporaryFocusPower : PowerModel, ITemporaryPower
{
    public override PowerType Type => IsPositive ? PowerType.Buff : PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public abstract AbstractModel OriginModel { get; }

    public PowerModel InternallyAppliedPower => ModelDb.Power<FocusPower>();

    protected virtual bool IsPositive => true;

    private int Sign => IsPositive ? 1 : -1;

    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        await PowerCmd.Apply<FocusPower>(target.CombatState!, target,
            Sign * amount, applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        if (amount != Amount && ReferenceEquals(power, this))
            await PowerCmd.Apply<FocusPower>(Owner.CombatState!, Owner,
                Sign * amount, applier, cardSource);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;
        await PowerCmd.Remove(this);
        await PowerCmd.Apply<FocusPower>(Owner.CombatState!, Owner,
            -Sign * Amount, Owner, cardSource: null);
    }
}
