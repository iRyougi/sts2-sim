using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BlackBlood : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override Task AfterCombatVictory() => Owner.Creature.IsDead
        ? Task.CompletedTask
        : CreatureCmd.Heal(Owner.Creature, 12m);
}
