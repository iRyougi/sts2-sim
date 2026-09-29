using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>VeilpiercerPower</c>：持有者在手牌或打出区的虚无牌能量费用变为 0（Late 阶段）；
/// 每打出一张这样的虚无牌减 1 层。</summary>
public sealed class VeilpiercerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner.Creature != Owner ||
            !card.Keywords.Contains(CardKeyword.Ethereal) ||
            !IsInHandOrPlay(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner &&
            cardPlay.Card.Keywords.Contains(CardKeyword.Ethereal) &&
            IsInHandOrPlay(cardPlay.Card))
        {
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        }
    }

    private static bool IsInHandOrPlay(CardModel card) =>
        card.Pile?.Type is PileType.Hand or PileType.Play;
}
