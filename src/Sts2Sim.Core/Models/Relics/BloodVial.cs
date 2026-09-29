using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BloodVial : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterPlayerTurnStartLate(Player player)
    {
        if (ReferenceEquals(player, Owner) &&
            Owner.PlayerCombatState?.TurnNumber == 1)
        {
            await CreatureCmd.Heal(Owner.Creature, 2m);
        }
    }
}
