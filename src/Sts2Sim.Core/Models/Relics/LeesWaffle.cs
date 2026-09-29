using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LeesWaffle : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        Owner.Creature.SetMaxHpInternal(Owner.Creature.MaxHp + 7);
        Owner.Creature.HealInternal(Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
        return Task.CompletedTask;
    }
}
