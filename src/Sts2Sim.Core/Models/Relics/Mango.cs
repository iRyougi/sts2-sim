using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Mango : RelicModel
{
    private const int HpGranted = 14;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        Owner.Creature.SetMaxHpInternal(Owner.Creature.MaxHp + HpGranted);
        Owner.Creature.HealInternal(HpGranted);
        return Task.CompletedTask;
    }
}
