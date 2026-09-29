using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class IceCream : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool ShouldPlayerResetEnergy(Player player) =>
        player != Owner ||
        player.PlayerCombatState?.TurnNumber < 1;
}
