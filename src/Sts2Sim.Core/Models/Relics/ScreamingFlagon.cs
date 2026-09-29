using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ScreamingFlagon : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) ||
            Owner.PlayerCombatState?.Hand.Cards.Count != 0)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        await CreatureCmd.Damage(
            combatState,
            combatState.HittableEnemies.ToArray(),
            20m,
            ValueProp.Unpowered,
            Owner.Creature,
            cardSource: null,
            cardPlay: null);
    }
}
