using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Sai : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants) =>
        participants.Contains(Owner.Creature)
            ? CreatureCmd.GainBlock(Owner.Creature.CombatState!, Owner.Creature, 7m, ValueProp.Unpowered, null, null)
            : Task.CompletedTask;
}
