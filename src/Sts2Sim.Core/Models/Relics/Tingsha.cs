using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Tingsha : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterCardDiscarded(CardModel card)
    {
        if (card.Owner != Owner || Owner.Creature.Side != Owner.Creature.CombatState!.CurrentSide)
        {
            return;
        }

        Creature? target = Owner.RunState.Rng.CombatTargets.NextItem(
            Owner.Creature.CombatState.HittableEnemies);
        if (target is not null)
        {
            await CreatureCmd.Damage(
                Owner.Creature.CombatState, new[] { target }, 3m, ValueProp.Unpowered,
                Owner.Creature, null, null);
        }
    }
}
