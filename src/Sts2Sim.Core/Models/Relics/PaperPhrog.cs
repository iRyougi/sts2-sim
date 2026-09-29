using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Increases the vulnerable multiplier of the owner's powered attacks by 0.25.</summary>
public sealed class PaperPhrog : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public decimal ModifyVulnerableMultiplier(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = cardSource;
        if (ReferenceEquals(target, Owner.Creature) || !props.IsPoweredAttack())
            return amount;
        return amount + 0.25m;
    }
}
