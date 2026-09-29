using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MercuryHourglass : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        // #118 closed: dispatch in the normal player-start phase after drawing.
        if (player != Owner)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        await CreatureCmd.Damage(
            combatState,
            combatState.HittableEnemies.ToArray(),
            3m,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }
}
