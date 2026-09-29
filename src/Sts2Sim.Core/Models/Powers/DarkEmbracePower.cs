using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class DarkEmbracePower : PowerModel
{
    private int _etherealExhausts;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner) return;
        if (causedByEthereal) _etherealExhausts++;
        else await CardPileCmd.Draw(Owner.CombatState!, Amount, Owner.Player!, fromHandDraw: false);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        int deferred = _etherealExhausts;
        _etherealExhausts = 0;
        await CardPileCmd.Draw(Owner.CombatState!, Amount * deferred, Owner.Player!, fromHandDraw: false);
    }

    internal override void AppendCombatStateDescription(ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => builder.Append(_etherealExhausts);
}
