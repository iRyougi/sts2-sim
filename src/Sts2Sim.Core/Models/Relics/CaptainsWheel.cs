using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class CaptainsWheel : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterBlockCleared(Creature creature)
    {
        if (creature != Owner.Creature ||
            Owner.PlayerCombatState?.TurnNumber != 2)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(
            creature.CombatState!,
            creature,
            18m,
            ValueProp.Unpowered,
            null,
            null);
    }
}
