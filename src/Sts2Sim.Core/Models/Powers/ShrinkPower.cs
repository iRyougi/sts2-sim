using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ShrinkPower : PowerModel
{
    private const decimal DamageMultiplier = 0.70m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType =>
        Amount < 0 ? PowerStackType.Single : PowerStackType.Counter;

    public override bool AllowNegative => true;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        dealer == Owner && props.IsPoweredAttack()
            ? DamageMultiplier
            : 1m;

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Amount >= 0 && participants.Contains(Owner))
        {
            await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        }
    }

    public override async Task AfterDeath(Creature target)
    {
        if (ReferenceEquals(target, Applier) && Owner.Powers.Contains(this))
        {
            await PowerCmd.Remove(this);
        }
    }
}
