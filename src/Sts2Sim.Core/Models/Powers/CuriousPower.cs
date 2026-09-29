using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Reduces the energy cost of the owner's Power cards by its amount, to a floor of zero.</summary>
public sealed class CuriousPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        if (card.Owner.Creature != Owner || card.Type != CardType.Power || originalCost <= 0m)
        {
            modifiedCost = originalCost;
            return false;
        }
        modifiedCost = Math.Max(0m, originalCost - Amount);
        return true;
    }
}
