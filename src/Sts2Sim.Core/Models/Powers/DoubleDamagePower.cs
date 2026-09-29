using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class DoubleDamagePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        dealer == Owner && props.IsPoweredAttack() && cardSource is not null ? 2m : 1m;
    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
    }
}
