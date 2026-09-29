using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Pear : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        Owner.Creature.SetMaxHpInternal(Owner.Creature.MaxHp + 10);
        Owner.Creature.HealInternal(10m);
        return Task.CompletedTask;
    }
}
