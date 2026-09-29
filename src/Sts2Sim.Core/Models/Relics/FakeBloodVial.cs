using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeBloodVial : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override int MerchantCost => 50;

    public override Task AfterPlayerTurnStartLate(Player player) =>
        ReferenceEquals(player, Owner) &&
        Owner.PlayerCombatState?.TurnNumber == 1
            ? CreatureCmd.Heal(Owner.Creature, 1m)
            : Task.CompletedTask;
}
