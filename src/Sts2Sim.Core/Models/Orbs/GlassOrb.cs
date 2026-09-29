using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Orbs;

public sealed class GlassOrb : OrbModel
{
    private decimal _passiveVal = 4m;

    public override decimal PassiveVal => ModifyOrbValue(_passiveVal);

    public override decimal EvokeVal => PassiveVal * 2m;

    internal decimal RawPassiveValue => _passiveVal;

    public override Task BeforeTurnEndOrbTrigger(ICombatState combatState) =>
        TriggerPassive(combatState, null);

    public override async Task Passive(ICombatState combatState, Creature? target)
    {
        List<Creature> targets = combatState.HittableEnemies
            .Where(enemy => enemy.IsHittable).ToList();
        decimal value = PassiveVal;
        if (value <= 0m)
            return;
        ActivatePassive();
        _passiveVal = Math.Max(0m, _passiveVal - 1m);
        await CreatureCmd.Damage(combatState, targets, value, ValueProp.Unpowered,
            Owner.Creature, cardSource: null, cardPlay: null);
    }

    public override async Task<IReadOnlyList<Creature>> Evoke(ICombatState combatState)
    {
        List<Creature> targets = combatState.HittableEnemies
            .Where(enemy => enemy.IsHittable).ToList();
        if (EvokeVal <= 0m)
            return [];
        ActivateEvoke(targets);
        await CreatureCmd.Damage(combatState, targets, EvokeVal, ValueProp.Unpowered,
            Owner.Creature, cardSource: null, cardPlay: null);
        return targets;
    }
}
