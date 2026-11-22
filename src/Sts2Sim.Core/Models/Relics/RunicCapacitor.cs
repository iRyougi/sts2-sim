using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RunicCapacitor : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants)
    {
        // game PlayerCombatState.TurnNumber starts at 1: the first player turn is turn 1.
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState!.TurnNumber != 1)
            return;
        Flash();
        await OrbCmd.AddSlots(Owner, 3);
    }
}
