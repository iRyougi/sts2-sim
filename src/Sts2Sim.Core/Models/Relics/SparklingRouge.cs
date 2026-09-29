using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SparklingRouge : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (creature != Owner.Creature ||
            Owner.PlayerCombatState?.TurnNumber != 2)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            creature.CombatState!,
            creature,
            1m,
            creature,
            null);
        await PowerCmd.Apply<DexterityPower>(
            creature.CombatState!,
            creature,
            1m,
            creature,
            null);
    }
}
