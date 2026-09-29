using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ConcoctPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer == Owner && props.IsPoweredAttack() && result.UnblockedDamage > 0)
        {
            await PowerCmd.Apply<PoisonPower>(Owner.CombatState!, target, Amount, Owner, null);
        }
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        side != Owner.Side ? PowerCmd.Remove(this) : Task.CompletedTask;
}
