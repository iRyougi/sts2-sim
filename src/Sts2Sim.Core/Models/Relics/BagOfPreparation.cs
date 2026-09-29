using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BagOfPreparation : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) =>
        player == Owner && player.PlayerCombatState?.TurnNumber == 1
            ? originalCardCount + 2m
            : originalCardCount;
}
