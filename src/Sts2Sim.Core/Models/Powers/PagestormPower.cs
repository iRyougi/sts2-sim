using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>PagestormPower</c>：持有者每抽到一张虚无牌，再抽层数张（非回合抽牌）。</summary>
public sealed class PagestormPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (card.Owner.Creature == Owner && card.Keywords.Contains(CardKeyword.Ethereal))
            await CardPileCmd.Draw(Owner.CombatState!, Amount, Owner.Player!, fromHandDraw: false);
    }
}
