using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaperKrane : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public decimal ModifyWeakMultiplier(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        target == Owner.Creature && props.IsPoweredAttack() ? amount - 0.15m : amount;
}
