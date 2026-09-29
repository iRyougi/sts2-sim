using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Damages its owner for four unblockable, unpowered damage on turn one.</summary>
public sealed class RoyalPoison : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        // #251 closed: normal player-start damage precedes late healing callbacks.
        if (player != Owner || Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        await CreatureCmd.Damage(
            Owner.Creature.CombatState!,
            [Owner.Creature],
            4m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null,
            null);
    }
}
