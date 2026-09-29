using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LunarPastry : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants) =>
        participants.Contains(Owner.Creature)
            ? PlayerCmd.GainStars(1m, Owner)
            : Task.CompletedTask;
}
