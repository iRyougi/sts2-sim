using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class VitruvianMinion : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        IsOwnedMinion(cardSource) ? 2m : 1m;

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        IsOwnedMinion(cardSource) ? 2m : 1m;

    private bool IsOwnedMinion(CardModel? card) =>
        card?.Owner == Owner && card.Tags.Contains(CardTag.Minion);
}
