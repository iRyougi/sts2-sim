using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ShadowmeldPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        target == Owner ? (decimal)Math.Pow(2d, Amount) : 1m;

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) =>
        participants.Contains(Owner) ? PowerCmd.Remove(this) : Task.CompletedTask;
}
