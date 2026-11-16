using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 Thermae Nonupeipe relic (#927): heal 8 at the late start of the owner's first turn.</summary>
public sealed class BathTowel : RelicModel
{
    private const decimal Heal = 8m;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterPlayerTurnStartLate(Player player)
    {
        if (player == Owner && Owner.PlayerCombatState!.TurnNumber <= 1)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, Heal);
        }
    }
}
