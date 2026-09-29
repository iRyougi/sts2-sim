using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class NostalgiaPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (card.Owner.Creature != Owner ||
            card.Type is not (CardType.Attack or CardType.Skill) ||
            location.PileType != PileType.Discard ||
            card.Owner.PlayerCombatState!.AttackOrSkillCardPlaysStartedThisTurn >= Amount)
        {
            return location;
        }

        return location with
        {
            PileType = PileType.Draw,
            Position = CardPilePosition.Top,
        };
    }
}
