using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HornCleat : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterBlockCleared(Creature creature)
    {
        if (creature != Owner.Creature ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(
            creature.CombatState!,
            creature,
            14m,
            ValueProp.Unpowered,
            null,
            null);
    }
}
