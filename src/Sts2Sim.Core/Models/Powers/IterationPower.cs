using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class IterationPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner || card.Type != CardType.Status) return;
        var state = (CombatState)Owner.CombatState!;
        int drawnStatuses = state.SemanticHistory.CountStatusCardsDrawnThisTurn(state, Owner.Player!);
        if (drawnStatuses <= 1)
            await CardPileCmd.Draw(state, Amount, Owner.Player!, fromHandDraw: false);
    }
}
