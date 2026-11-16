using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 Museum of Torture relic (#927): the owner's multi-hit Attack cards hit one extra time.</summary>
public sealed class Thumbscrew : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override int ModifyAttackHitCount(AttackCommand command, int hitCount)
    {
        if (hitCount <= 1)
        {
            return hitCount;
        }

        if (command.ModelSource is not CardModel { Type: CardType.Attack } card)
        {
            return hitCount;
        }

        return card.Owner != Owner ? hitCount : hitCount + 1;
    }
}
