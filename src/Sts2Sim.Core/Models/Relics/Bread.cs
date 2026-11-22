using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Bread : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner || Owner.PlayerCombatState is null || Owner.PlayerCombatState.TurnNumber <= 1)
        {
            return amount;
        }

        return amount + 1m;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState is null)
        {
            return Task.CompletedTask;
        }

        if (Owner.PlayerCombatState.TurnNumber == 1)
        {
            return PlayerCmd.LoseEnergy(2m, Owner);
        }

        return Task.CompletedTask;
    }
}
