using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SpikedGauntlets : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = card.Owner == Owner && card.Type == CardType.Power ? originalCost + 1m : originalCost;
        return modifiedCost != originalCost;
    }
}
