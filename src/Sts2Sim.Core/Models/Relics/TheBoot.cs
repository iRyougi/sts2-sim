using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TheBoot : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource) =>
        (dealer == Owner.Creature || dealer == Owner.Osty) && target != Owner.Creature &&
        props.IsPoweredAttack() && amount >= 1m && amount < 5m
            ? 5m
            : amount;
}
