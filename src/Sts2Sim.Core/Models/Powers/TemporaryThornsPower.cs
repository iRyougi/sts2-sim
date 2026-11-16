using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>提供临时荆棘直至敌方回合结束（原版 <c>TemporaryThornsPower</c>）。</summary>
public abstract class TemporaryThornsPower : PowerModel, ITemporaryPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public abstract AbstractModel OriginModel { get; }
    public PowerModel InternallyAppliedPower => ModelDb.Power<ThornsPower>();

    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        await PowerCmd.Apply<ThornsPower>(target.CombatState!, target, amount, applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this) && amount != Amount)
        {
            await PowerCmd.Apply<ThornsPower>(Owner.CombatState!, Owner, amount, applier, cardSource);
        }
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side)
        {
            await PowerCmd.Remove(this);
            if (Owner.GetPower<ThornsPower>() is { } thorns)
            {
                await PowerCmd.ModifyAmount(Owner.CombatState!, thorns, -Amount, Owner, null);
            }
        }
    }
}
