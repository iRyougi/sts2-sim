using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeLeesWaffle : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    public override int MerchantCost => 50;

    public override Task AfterObtained() =>
        CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp * 0.1m);
}
