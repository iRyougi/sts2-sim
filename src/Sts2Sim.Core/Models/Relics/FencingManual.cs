using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FencingManual : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        return participants.Contains(Owner.Creature) &&
               Owner.PlayerCombatState?.TurnNumber == 1
            ? ForgeCmd.Forge(10m, Owner, this)
            : Task.CompletedTask;
    }
}
