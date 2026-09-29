using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Orbs;

public sealed class LightningOrb : OrbModel
{
    public override decimal PassiveVal => ModifyOrbValue(3m);

    public override decimal EvokeVal => ModifyOrbValue(8m);

    public override Task BeforeTurnEndOrbTrigger(ICombatState combatState) =>
        TriggerPassive(combatState, null);

    public override async Task Passive(ICombatState combatState, Creature? target)
    {
        ActivatePassive();
        await ApplyLightningDamage(combatState, PassiveVal, target, isEvoke: false);
    }

    public override Task<IReadOnlyList<Creature>> Evoke(ICombatState combatState) =>
        ApplyLightningDamage(combatState, EvokeVal, null, isEvoke: true);

    private async Task<IReadOnlyList<Creature>> ApplyLightningDamage(
        ICombatState combatState, decimal value, Creature? target, bool isEvoke)
    {
        List<Creature> candidates = combatState.GetOpponentsOf(Owner.Creature)
            .Where(enemy => enemy.IsHittable).ToList();
        if (candidates.Count == 0)
            return [];

        Creature selected = target ?? Owner.RunState.Rng.CombatTargets.NextItem(candidates)!;
        Creature[] targets = [selected];
        if (isEvoke)
            ActivateEvoke(targets);
        await CreatureCmd.Damage(combatState, targets, value, ValueProp.Unpowered,
            Owner.Creature, cardSource: null, cardPlay: null);
        return targets;
    }
}
