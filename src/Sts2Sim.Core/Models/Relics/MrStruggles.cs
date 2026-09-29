using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MrStruggles : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.Damage(
            Owner.Creature.CombatState!,
            Owner.Creature.CombatState!.HittableEnemies,
            Owner.PlayerCombatState!.TurnNumber,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }
}
