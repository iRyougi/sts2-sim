using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MeatOnTheBone : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCombatVictoryEarly()
    {
        if (!Owner.Creature.IsAlive ||
            Owner.Creature.CurrentHp > Owner.Creature.MaxHp * 0.5m)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.Heal(Owner.Creature, 12m);
    }
}
