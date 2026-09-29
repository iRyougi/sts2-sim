using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeMango : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    public override int MerchantCost => 50;

    public override Task AfterObtained() =>
        CreatureCmd.GainMaxHp(Owner.Creature, 3m);
}
