using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RingOfTheDrake : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        ReferenceEquals(player, Owner) && Owner.PlayerCombatState?.TurnNumber <= 3
            ? count + 2m
            : count;
}
