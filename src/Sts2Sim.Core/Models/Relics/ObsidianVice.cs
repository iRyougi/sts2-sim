using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 Museum of Torture relic (#927): before the owner's side turn ends, deal 1 unpowered damage per
/// card in hand to a random hittable enemy.</summary>
public sealed class ObsidianVice : RelicModel
{
    private const decimal DamagePerCard = 1m;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return;
        }

        int count = Owner.PlayerCombatState!.Hand.Cards.Count;
        if (count == 0)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        Creature? target = combatState.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
        if (target is null)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(combatState, new[] { target }, DamagePerCard * count, ValueProp.Unpowered,
            Owner.Creature, null, null);
    }
}
