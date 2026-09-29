using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class CorruptionPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner.Creature != Owner || card.Type != CardType.Skill) return false;
        modifiedCost = 0m;
        return true;
    }

    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay,
        ResourceInfo resources, CardLocation location) =>
        card.Owner.Creature == Owner && card.Type == CardType.Skill
            ? location with { PileType = PileType.Exhaust }
            : location;
}
