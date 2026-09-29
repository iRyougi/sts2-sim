using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsFlesh : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player == Owner && player.PlayerCombatState?.TurnNumber >= 3 ? amount + 1m : amount;
}
