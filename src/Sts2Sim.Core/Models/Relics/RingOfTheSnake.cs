using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>On the owner's first combat turn, increases the hand draw by two cards.</summary>
public sealed class RingOfTheSnake : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) =>
        ReferenceEquals(player, Owner) && player.PlayerCombatState?.TurnNumber == 1
            ? originalCardCount + 2m
            : originalCardCount;
}
