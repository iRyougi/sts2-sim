using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DivineDestiny : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants) =>
        participants.Contains(Owner.Creature) && Owner.PlayerCombatState!.TurnNumber <= 1
            ? PlayerCmd.GainStars(7m, Owner)
            : Task.CompletedTask;
}
