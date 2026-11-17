using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsFlesh : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player == Owner && player.PlayerCombatState?.TurnNumber >= 3 ? amount + 1m : amount;

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        // Source also returns early when Status is already Active (idempotent re-assignment), not reproduced.
        if (participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState?.TurnNumber >= 3)
        {
            Status = RelicStatus.Active;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }
}
