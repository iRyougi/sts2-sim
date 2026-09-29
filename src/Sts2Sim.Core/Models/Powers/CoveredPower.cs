using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// Prevents powered attack damage to the owner until the enemy side ends or the applier dies.
/// </summary>
public sealed class CoveredPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        target == Owner && props.IsPoweredAttack() ? 0m : 1m;

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && Owner.Powers.Contains(this))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterDeath(Creature target)
    {
        if (target == Applier && Owner.Powers.Contains(this))
        {
            await PowerCmd.Remove(this);
        }
    }
}
