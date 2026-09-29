using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Authoritative Free Skill: zeroes the next owned Skill in hand/play and consumes one stack before play.</summary>
public sealed class FreeSkillPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner.Creature != Owner || card.Type != CardType.Skill || card.Pile?.Type is not (PileType.Hand or PileType.Play))
        {
            return false;
        }
        modifiedCost = 0m;
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner && cardPlay.Card.Type == CardType.Skill &&
            cardPlay.Card.Pile?.Type is PileType.Hand or PileType.Play)
        {
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        }
    }
}
