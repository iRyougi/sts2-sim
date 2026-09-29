using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ReptileTrinket : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterPotionUsed(PotionModel potion, Player player)
    {
        if (player != Owner ||
            Owner.Creature.CombatState is not { } combatState ||
            !combatState.IsLiveCombat())
        {
            return;
        }

        await PowerCmd.Apply<ReptileTrinketPower>(
            combatState,
            Owner.Creature,
            3m,
            Owner.Creature,
            null);
    }
}
