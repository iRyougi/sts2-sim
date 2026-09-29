using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Heal the living owner for six after combat victory.</summary>
public sealed class BurningBlood : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override Task AfterCombatVictory() => Owner.Creature.IsDead
        ? Task.CompletedTask
        : CreatureCmd.Heal(Owner.Creature, 6m);
}
