using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>使持有者接下来的 N 张手牌/结算中卡牌费用变为 0（原版 <c>FreeCardPower</c>）。</summary>
public sealed class FreeCardPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card.Owner.Creature, Owner) ||
            card.Pile?.Type is not (PileType.Hand or PileType.Play))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner.Creature, Owner) &&
            cardPlay.Card.Pile?.Type is PileType.Hand or PileType.Play)
        {
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        }
    }
}
