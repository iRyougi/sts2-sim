using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TungstenRod : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override decimal ModifyHpLost(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return target == Owner.Creature ? Math.Max(0m, amount - 1m) : amount;
    }
}
