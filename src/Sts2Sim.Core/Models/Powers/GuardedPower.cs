using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class GuardedPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override async Task AfterDeath(Creature target, bool wasRemovalPrevented)
    {
        if (!wasRemovalPrevented && ReferenceEquals(target, Applier))
            await PowerCmd.Remove(this);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay) =>
        ReferenceEquals(target, Owner) && props.IsPoweredAttack() ? 0.5m : 1m;
}
