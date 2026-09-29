using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ParryingShield : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) ||
            Owner.Creature.Block < 10)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        Creature? target = combatState.RunState.Rng.CombatTargets.NextItem(
            combatState.HittableEnemies);
        if (target is null)
        {
            return;
        }

        await CreatureCmd.Damage(
            combatState,
            new[] { target },
            6m,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }
}
