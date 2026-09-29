using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class TankPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (Creature teammate in Owner.CombatState!.Allies)
        {
            if (teammate.IsAlive && teammate.IsPlayer && !ReferenceEquals(teammate, Owner))
                await PowerCmd.Apply<GuardedPower>(Owner.CombatState, teammate,
                    Amount, Owner, null);
        }
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay) =>
        ReferenceEquals(target, Owner) && props.IsPoweredAttack() ? 1.5m : 1m;
}
