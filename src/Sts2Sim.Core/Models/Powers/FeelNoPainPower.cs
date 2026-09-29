using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class FeelNoPainPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCardExhausted(CardModel card, bool causedByEthereal) =>
        card.Owner.Creature == Owner
            ? CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount,
                ValueProp.Unpowered, null, null)
            : Task.CompletedTask;
}
