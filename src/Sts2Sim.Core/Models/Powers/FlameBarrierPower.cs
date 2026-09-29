using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class FlameBarrierPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer is not null && props.IsPoweredAttack())
        {
            await CreatureCmd.Damage(Owner.CombatState!, [dealer], Amount,
                ValueProp.Unpowered, Owner, null, null);
        }
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        Owner.Side != side ? PowerCmd.Remove(this) : Task.CompletedTask;
}
