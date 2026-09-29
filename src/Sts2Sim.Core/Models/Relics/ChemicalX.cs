using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ChemicalX : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override int ModifyXValue(CardModel card, int originalValue) =>
        card.Owner == Owner ? originalValue + 2 : originalValue;
}
