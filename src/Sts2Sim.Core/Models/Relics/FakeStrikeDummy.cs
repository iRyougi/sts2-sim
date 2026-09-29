using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeStrikeDummy : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override int MerchantCost => 50;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        (dealer == Owner.Creature || cardSource?.Owner == Owner) &&
        cardSource?.Tags.Contains(CardTag.Strike) == true &&
        props.IsPoweredAttack()
            ? 1m
            : 0m;
}
